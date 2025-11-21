using NLua;

namespace Core
{
    public class Scripting
    {
        public void Execute(string script)
        {
            using (Lua lua = new Lua())
            {
                lua.DoString(script);
            }
        }
    }
}
