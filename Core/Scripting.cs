using NLua;
using System.IO;

namespace Core
{
    public class Scripting
    {
        private Lua lua;

        public Scripting()
        {
            lua = new Lua();
        }

        public void Execute(string script)
        {
            lua.DoString(script);
        }

        public void ExecuteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                var script = File.ReadAllText(filePath);
                Execute(script);
            }
        }

        public void Dispose()
        {
            lua.Dispose();
        }
    }
}
