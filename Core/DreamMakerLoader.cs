using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Core.VM.Procs;
using Core.VM.Runtime;
using Shared.Compiler;

namespace Core
{
    public class DreamMakerLoader : IDreamMakerLoader
    {
        private readonly IObjectTypeManager _typeManager;
        private readonly IDreamVM? _dreamVM;
        private readonly ICompiledJsonService _jsonService;

        public DreamMakerLoader(IObjectTypeManager typeManager, ICompiledJsonService jsonService, IDreamVM? dreamVM = null)
        {
            _typeManager = typeManager;
            _jsonService = jsonService;
            _dreamVM = dreamVM;
        }

        public void Load(ICompiledJson compiledJson)
        {
            if (_dreamVM != null)
            {
                _jsonService.LoadStrings(compiledJson, _dreamVM.Strings);
                _jsonService.LoadProcs(compiledJson, _dreamVM.Procs);
            }

            _jsonService.LoadTypes(compiledJson, _typeManager);
        }
    }
}
