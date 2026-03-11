using System.Collections.Generic;

using Shared.Interfaces;

namespace Shared;
    public interface IScriptApi : IApiProvider
    {
        List<string> ListScriptFiles();
        bool ScriptFileExists(string filename);
        string ReadScriptFile(string filename);
        void WriteScriptFile(string filename, string content);
        void DeleteScriptFile(string filename);

        /// <summary>
        /// Triggers a hot-reload of all engine scripts.
        /// </summary>
        Task HotReloadAsync();
    }
