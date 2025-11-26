using Core;
using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace Server
{
    public class ScriptHost : IDisposable
    {
        private readonly Scripting _scripting;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private readonly object _scriptLock = new object();
        private readonly GameState _gameState;
        private readonly GameApi _gameApi;
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly OpenDreamCompilerService _compilerService;

        public ScriptHost()
        {
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _compilerService = new OpenDreamCompilerService();

            var mapLoader = new MapLoader(_objectTypeManager);
            _gameApi = new GameApi(_gameState, _objectTypeManager, mapLoader);
            _scripting = new Scripting(_gameApi);

            _watcher = new FileSystemWatcher(Constants.ScriptsRoot)
            {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _debounceTimer = new Timer(ReloadScripts, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            Console.WriteLine("Starting script host...");
            ReloadScripts(null); // Initial script load
            _watcher.Changed += OnScriptChanged;
            Console.WriteLine($"Watching for changes in '{Constants.ScriptsRoot}' directory.");
        }

        private void OnScriptChanged(object source, FileSystemEventArgs e)
        {
            var ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext == ".lua" || ext == ".dm")
            {
                Console.WriteLine($"File {e.FullPath} has been changed. Debouncing reload...");
                _debounceTimer.Change(200, Timeout.Infinite);
            }
        }

        private void ReloadScripts(object? state)
        {
            lock (_scriptLock)
            {
                try
                {
                    Console.WriteLine("Reloading scripts...");

                    // 1. Clear existing types for a clean reload
                    _objectTypeManager.Clear();

                    // 2. Compile DM files
                    if (Directory.Exists(Constants.ScriptsRoot))
                    {
                        var dmFiles = Directory.GetFiles(Constants.ScriptsRoot, "*.dm", SearchOption.AllDirectories).ToList();
                        if (dmFiles.Any())
                        {
                            dmFiles.Sort(); // Ensure a predictable order
                            Console.WriteLine($"Found {dmFiles.Count} DM files to compile.");
                            var compiledJsonPath = _compilerService.Compile(dmFiles);

                            if (compiledJsonPath != null && File.Exists(compiledJsonPath))
                            {
                                Console.WriteLine($"Compilation successful. Loading from {compiledJsonPath}");
                                var loader = new DreamMakerLoader(_objectTypeManager);
                                loader.Load(compiledJsonPath);

                                // Clean up the compiled file
                                try { File.Delete(compiledJsonPath); }
                                catch (IOException ex) { Console.WriteLine($"Warning: Could not delete compiled file {compiledJsonPath}: {ex.Message}"); }
                            }
                            else
                            {
                                Console.WriteLine("DM compilation failed or produced no output. Aborting script reload.");
                                return;
                            }
                        }
                    }

                    // 3. Reload and execute Lua scripts
                    _scripting.Reload();

                    var mainLua = Path.Combine(Constants.ScriptsRoot, "main.lua");
                    if(File.Exists(mainLua))
                    {
                        _scripting.ExecuteFile(mainLua);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing script: {ex.Message}");
                }
            }
        }

        public string ExecuteCommand(string command)
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

        public void Dispose()
        {
            _watcher.Changed -= OnScriptChanged;
            _watcher.Dispose();
            _debounceTimer.Dispose();
            _scripting.Dispose();
        }
    }
}
