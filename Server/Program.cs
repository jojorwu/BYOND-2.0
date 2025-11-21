using Core;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Scripting scripting = new Scripting();
            scripting.Execute(@"print('Hello from Lua!')");
        }
    }
}
