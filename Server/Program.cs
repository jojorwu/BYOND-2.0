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
            using (scripting = new Scripting())
            {
                // Initial script execution
                scripting.ExecuteFile("scripts/main.lua");

                using (FileSystemWatcher watcher = new FileSystemWatcher())
                {
                    watcher.Path = "scripts";
                    watcher.Filter = "*.lua";
                    watcher.NotifyFilter = NotifyFilters.LastWrite;
                    watcher.Changed += OnChanged;
                    watcher.EnableRaisingEvents = true;

                    Console.WriteLine("Watching for changes in the 'scripts' directory. The server is running. Press Enter to exit.");
                    Console.ReadLine();
                }
            }
        }

        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                Console.WriteLine($"File {e.FullPath} has been changed. Reloading...");
                scripting.ExecuteFile(e.FullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing script: {ex.Message}");
            }
        }
    }
}
