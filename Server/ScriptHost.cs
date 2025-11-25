using Core;
using System;
using System.IO;
using System.Threading;
using System.Linq;

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
private readonly DmParser _dmParser;

public ScriptHost()
    {
        _gameState = new GameState();
        _objectTypeManager = new ObjectTypeManager();
        _dmParser = new DmParser(_objectTypeManager);

        var mapLoader = new MapLoader(_objectTypeManager);
        _gameApi = new GameApi(_gameState, _objectTypeManager, mapLoader);
        _scripting = new Scripting(_gameApi);

        _watcher = new FileSystemWatcher(Constants.ScriptsRoot)
        {
            Filter = "*.*", // Watch all files, we filter extensions in ReloadScripts
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

                _objectTypeManager.Clear();

                // 1. Load DM definitions first
                if (Directory.Exists(Constants.ScriptsRoot))
                {
                    var dmFiles = Directory.GetFiles(Constants.ScriptsRoot, "*.dm", SearchOption.AllDirectories);
                    foreach (var dmFile in dmFiles)
                    {
                        Console.WriteLine($"Parsing DM: {Path.GetFileName(dmFile)}");
                        try
                        {
                            _dmParser.ParseFile(dmFile);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing DM file {dmFile}: {ex.Message}");
                        }
                    }
                }

                // 2. Reload Lua environment
                _scripting.Reload();

                // 3. Execute Main.lua
                var mainLua = Path.Combine(Constants.ScriptsRoot, "main.lua");
                if (File.Exists(mainLua))
                {
                    _scripting.ExecuteFile(mainLua);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reloading scripts: {ex.Message}");
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
