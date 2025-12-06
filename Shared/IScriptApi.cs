using System.Collections.Generic;

namespace Core
{
    public interface IScriptApi
    {
        List<string> ListScriptFiles();
        bool ScriptFileExists(string filename);
        string ReadScriptFile(string filename);
        void WriteScriptFile(string filename, string content);
        void DeleteScriptFile(string filename);
    }
}
