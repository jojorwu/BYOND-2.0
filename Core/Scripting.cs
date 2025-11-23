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

        public void ExecuteString(string script)
        {
            lock (luaLock)
            {
                lua.DoString(script);
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
