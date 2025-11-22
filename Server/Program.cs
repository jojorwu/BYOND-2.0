using Core;
using System;
using System.IO;
using System.Threading;

namespace Server
{
    class Program
    {
        private static Scripting scripting;

        static void Main(string[] args)
        {
            // Initialize the scripting engine.
            scripting = new Scripting();

            // Execute the main script file to start the game.
            scripting.ExecuteFile("scripts/main.lua");

            // Set up a file system watcher to monitor the 'scripts' directory for changes.
            // This enables hot-reloading of Lua scripts.
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = "scripts";
            watcher.Filter = "*.lua"; // Only watch for changes in .lua files.
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += OnChanged;
            watcher.EnableRaisingEvents = true;

            Console.WriteLine("Watching for changes in the 'scripts' directory. The server is running.");

            // Keep the application running indefinitely.
            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Event handler for the FileSystemWatcher's Changed event.
        /// This method is called when a file in the 'scripts' directory is modified.
        /// </summary>
        /// <param name="source">The source of the event.</param>
        /// <param name="e">An object that contains the event data.</param>
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine($"File {e.FullPath} has been changed. Reloading...");
            // Re-execute the modified script.
            scripting.ExecuteFile(e.FullPath);
        }
    }
}
