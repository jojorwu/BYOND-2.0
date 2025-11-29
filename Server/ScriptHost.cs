using Core;
using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Core.VM.Runtime;
using DMCompiler.Json;
using LiteNetLib;

namespace Server
{
    public class ScriptHost : IDisposable
    {
        private readonly Project _project;
        private Scripting _scripting;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private readonly object _scriptLock = new object();
        private readonly GameState _gameState;
        private readonly GameApi _gameApi;
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly OpenDreamCompilerService _compilerService;
        private readonly DreamVM _dreamVM;
        private readonly ServerSettings _settings;
        private readonly List<DreamThread> _threads = new();
        private readonly System.Diagnostics.Stopwatch _stopwatch = new();
        private int _lastProcessedThreadIndex = -1;
        private readonly List<DreamThread> _completedThreads = new();
        private readonly ConcurrentQueue<Command> _commandQueue = new();
        private record Command(NetPeer Peer, string Text);

        public ScriptHost(Project project, GameState gameState, ServerSettings settings)
        {
            _project = project;
            _gameState = gameState;
            _settings = settings;
            _objectTypeManager = new ObjectTypeManager();
            _compilerService = new OpenDreamCompilerService(_project);
            _dreamVM = new DreamVM(settings);

            var mapLoader = new MapLoader(_objectTypeManager);
            _gameApi = new GameApi(_project, _gameState, _objectTypeManager, mapLoader);
            _scripting = new Scripting(_gameApi);

            var scriptsRoot = _project.GetFullPath(Constants.ScriptsRoot);
            if (!Directory.Exists(scriptsRoot))
            {
                Directory.CreateDirectory(scriptsRoot);
            }

            _watcher = new FileSystemWatcher(scriptsRoot)
            {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            _debounceTimer = new Timer(ReloadScripts, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            Console.WriteLine("Starting script host...");
            ReloadScripts(null); // Initial script load
            _watcher.Changed += OnScriptChanged;
            _watcher.Created += OnScriptChanged;
            _watcher.Deleted += OnScriptChanged;
            _watcher.Renamed += OnScriptChanged;
            Console.WriteLine($"Watching for changes in '{_project.GetFullPath(Constants.ScriptsRoot)}' directory.");
        }

        public void Tick()
        {
            ProcessCommandQueue();

            lock (_gameState.Lock)
            {
                lock (_scriptLock)
                {
                    var budgetSettings = _settings.Performance.TimeBudgeting.ScriptHost;
                if (!budgetSettings.Enabled)
                {
                    // Run all threads without a time budget
                    for (var i = _threads.Count - 1; i >= 0; i--)
                    {
                        var thread = _threads[i];
                        var state = thread.Run(_settings.Performance.VmInstructionSlice);
                        if (state != DreamThreadState.Running)
                        {
                            _threads.RemoveAt(i);
                        }
                    }
                    return;
                }

                var tickMilliseconds = 1000.0 / _settings.Performance.TickRate;
                var budgetMilliseconds = tickMilliseconds * budgetSettings.BudgetPercent;
                var budget = TimeSpan.FromMilliseconds(budgetMilliseconds);
                _stopwatch.Restart();

                if (_threads.Count == 0) return;

                var startIndex = (_lastProcessedThreadIndex + 1) % _threads.Count;
                DreamThread? lastProcessedThread = null;
                _completedThreads.Clear();

                for (int i = 0; i < _threads.Count; i++)
                {
                    int index = (startIndex + i) % _threads.Count;
                    var thread = _threads[index];

                    if (_stopwatch.Elapsed > budget && i > 0) // Always run at least one thread
                    {
                        break;
                    }

                    var state = thread.Run(_settings.Performance.VmInstructionSlice);
                    if (state != DreamThreadState.Running)
                    {
                        _completedThreads.Add(thread);
                    }
                    lastProcessedThread = thread;
                }

                if (_completedThreads.Count > 0)
                {
                    foreach (var completedThread in _completedThreads)
                    {
                        _threads.Remove(completedThread);
                    }
                }

                _lastProcessedThreadIndex = lastProcessedThread != null ? _threads.IndexOf(lastProcessedThread) : -1;
                }
            }
        }

        private void OnScriptChanged(object source, FileSystemEventArgs e)
        {
            var ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext == ".lua" || ext == ".dm" || ext == ".dmm")
            {
                Console.WriteLine($"File {e.FullPath} has been changed. Debouncing reload...");
                _debounceTimer.Change(200, Timeout.Infinite);
            }
        }

        private async void ReloadScripts(object? state)
        {
            Console.WriteLine("Compilation triggered...");
            var dmFiles = _project.GetDmFiles();
            if (dmFiles == null || !dmFiles.Any())
            {
                Console.WriteLine("No DM files found to compile.");
                return;
            }

            // Run compilation in a background thread to avoid freezing the server
            var compileResult = await Task.Run(() => _compilerService.Compile(dmFiles));

            // Lock now that we need to apply changes to the server state
            lock (_scriptLock)
            {
                try
                {
                    Console.WriteLine("Applying compilation results...");
                    _objectTypeManager.Clear();
                    _threads.Clear();

                    if (compileResult.messages.Any())
                    {
                        Console.WriteLine("Compilation finished with messages:");
                        foreach (var message in compileResult.messages)
                        {
                            Console.WriteLine(message);
                        }
                    }

                    if (compileResult.outputPath != null && File.Exists(compileResult.outputPath))
                    {
                        Console.WriteLine($"Compilation successful. Loading from {compileResult.outputPath}");
                        var json = File.ReadAllText(compileResult.outputPath);
                        var compiledJson = JsonSerializer.Deserialize<PublicDreamCompiledJson>(json);

                        if (compiledJson != null)
                        {
                            var loader = new DreamMakerLoader(_objectTypeManager, _project, _dreamVM);
                            loader.Load(compiledJson);
                            if (_settings.EnableVm)
                            {
                                var thread = _dreamVM.CreateWorldNewThread();
                                if (thread != null)
                                    _threads.Add(thread);
                            }
                        }

                        try { File.Delete(compileResult.outputPath); }
                        catch (IOException ex) { Console.WriteLine($"Warning: Could not delete compiled file {compileResult.outputPath}: {ex.Message}"); }
                    }
                    else
                    {
                        Console.WriteLine("DM compilation failed or produced no output. Skipping type and map loading.");
                    }

                    // Dispose the old scripting engine and create a new one for a clean slate
                    _scripting?.Dispose();
                    _scripting = new Scripting(_gameApi);

                    var mainLua = _project.GetFullPath(Path.Combine(Constants.ScriptsRoot, "main.lua"));
                    if (File.Exists(mainLua))
                    {
                        _scripting.ExecuteFile(mainLua);
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error deserializing compiled JSON: {ex.Message}");
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"File error during script reload: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred during script reload: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                }
            }
        }

        private void ProcessCommandQueue()
        {
            while (_commandQueue.TryDequeue(out var command))
            {
                string result;
                if (command.Text == "ping")
                {
                    result = "pong";
                }
                else
                {
                    result = ExecuteCommand(command.Text);
                }

                var writer = new NetDataWriter();
                writer.Put(result);
                command.Peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        private string ExecuteCommand(string command)
        {
            try
            {
                _scripting.ExecuteString(command);
                return "Command executed successfully.";
            }
            catch (Exception ex)
            {
                if (ex.Message == "Script execution timed out.")
                {
                    return "Script execution timed out.";
                }
                return $"Error executing command: {ex.Message}";
            }
        }

        public void EnqueueCommand(NetPeer peer, string command)
        {
            _commandQueue.Enqueue(new Command(peer, command));
        }

        public void Dispose()
        {
            _watcher.Changed -= OnScriptChanged;
            _watcher.Dispose();
            _debounceTimer.Dispose();
            _scripting.Dispose();
        }
    }
}
