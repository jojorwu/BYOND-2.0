using NLua;
using System.IO;

using System;

namespace Core
{
    public sealed class Scripting : IDisposable
    {
        private Lua lua;

        public Scripting()
        {
            lua = new Lua();
        }

        private void Execute(string script)
        {
            lua.DoString(script);
        }

        public void ExecuteFile(string filePath)
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
            Execute(script);
        }

        public void Dispose()
        {
            lua?.Dispose();
        }
    }
}
