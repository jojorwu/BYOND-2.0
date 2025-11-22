using Core;
using System;
using System.IO;
using System.Threading;

namespace Server
{
    public class ScriptHost : IDisposable
    {
        private readonly Scripting _scripting;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private readonly object _scriptLock = new object();
        private const string ScriptPath = "scripts";

        public ScriptHost()
        {
            _scripting = new Scripting();
            _watcher = new FileSystemWatcher(ScriptPath)
            {
                Filter = "*.lua",
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
            Console.WriteLine($"Watching for changes in '{ScriptPath}' directory.");
        }

        private void OnScriptChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine($"File {e.FullPath} has been changed. Debouncing reload...");
            _debounceTimer.Change(200, Timeout.Infinite);
        }

        private void ReloadScripts(object state)
        {
            lock (_scriptLock)
            {
                try
                {
                    Console.WriteLine("Reloading scripts...");
                    _scripting.Reload();
                    _scripting.ExecuteFile(Path.Combine(ScriptPath, "main.lua"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing script: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _watcher.Dispose();
            _debounceTimer.Dispose();
            _scripting.Dispose();
        }
    }
}
