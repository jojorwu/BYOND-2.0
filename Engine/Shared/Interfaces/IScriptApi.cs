using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Shared.Interfaces
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
