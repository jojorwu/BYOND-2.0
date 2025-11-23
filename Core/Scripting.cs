using NLua;
using System.IO;
using System;

namespace Core
{
    /// <summary>
    /// Manages the execution of Lua scripts.
    /// </summary>
    public sealed class Scripting : IDisposable
    {
        private Lua lua;
        private readonly object luaLock = new object();
        private readonly GameApi game;
        private readonly EditorApi? editor;

        /// <summary>
        /// Initializes a new instance of the <see cref="Scripting"/> class.
        /// </summary>
        public Scripting(GameApi game, EditorApi? editor = null)
        {
            this.game = game;
            this.editor = editor;
            lua = new Lua();
            RegisterApis();
        }

        private void RegisterApis()
        {
            lua["Game"] = game;
            if (editor != null)
            {
                lua["Editor"] = editor;
            }
        }

        public object[] ExecuteString(string script)
        {
            lock (luaLock)
            {
                return lua.DoString(script);
            }
        }

        public string ExecuteCommand(string command)
        {
            lock (luaLock)
            {
                var output = new System.Collections.Generic.List<string>();
                lua.RegisterFunction("print", this, GetType().GetMethod("CapturePrint"));
                CapturePrintContext.Value = output;

                var result = lua.DoString(command);
                var resultStrings = result.Select(r => r?.ToString() ?? "nil").ToArray();

                var allOutput = output.Concat(resultStrings);
                return string.Join("\n", allOutput);
            }
        }

        private static readonly System.Threading.AsyncLocal<System.Collections.Generic.List<string>> CapturePrintContext = new();

        public static void CapturePrint(params object[] args)
        {
            var output = CapturePrintContext.Value;
            if (output != null)
            {
                output.Add(string.Join("\t", args.Select(a => a?.ToString() ?? "nil")));
            }
        }

        /// <summary>
        /// Reloads the Lua state, providing a clean environment for script execution.
        /// </summary>
        public void Reload()
        {
            lock (luaLock)
            {
                lua.Close();
                lua = new Lua();
                RegisterApis();
            }
        }

        /// <summary>
        /// Executes a Lua script from a file.
        /// </summary>
        /// <param name="filePath">The path to the script file.</param>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file is not found.</exception>
        public void ExecuteFile(string? filePath)
        {
            if (filePath == null)
            {
                throw new System.ArgumentNullException(nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found.", filePath);
            }
            var script = File.ReadAllText(filePath);
            ExecuteString(script);
        }

        /// <summary>
        /// Releases the resources used by the Lua instance.
        /// </summary>
        public void Dispose()
        {
            lock (luaLock)
            {
                lua?.Dispose();
            }
        }
    }
}
