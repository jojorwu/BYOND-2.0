using NLua;
using System.IO;

namespace Core
{
    /// <summary>
    /// Manages Lua scripting within the engine.
    /// </summary>
    public class Scripting
    {
        private Lua lua;

        /// <summary>
        /// Initializes a new instance of the <see cref="Scripting"/> class.
        /// </summary>
        public Scripting()
        {
            lua = new Lua();
        }

        /// <summary>
        /// Executes a string containing Lua script.
        /// </summary>
        /// <param name="script">The Lua script to execute.</param>
        public void Execute(string script)
        {
            lua.DoString(script);
        }

        /// <summary>
        /// Executes a Lua script from a file.
        /// </summary>
        /// <param name="filePath">The path to the Lua script file.</param>
        public void ExecuteFile(string filePath)
        {
            if (filePath == null)
            {
                throw new System.ArgumentNullException(nameof(filePath));
            }

            if (File.Exists(filePath))
            {
                var script = File.ReadAllText(filePath);
                Execute(script);
            }
        }

        /// <summary>
        /// Releases the resources used by the Lua interpreter.
        /// </summary>
        public void Dispose()
        {
            lua.Dispose();
        }
    }
}
