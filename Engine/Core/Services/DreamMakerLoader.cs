using Shared;
using Shared.Compiler;
using System;

namespace Core
{
    public class DreamMakerLoader : IDreamMakerLoader
    {
        private readonly IObjectTypeManager _typeManager;
        private readonly IDreamVM? _dreamVM;
        private readonly ICompiledJsonService _compiledJsonService;
        private readonly IGameState _gameState;

        public DreamMakerLoader(IObjectTypeManager typeManager, ICompiledJsonService compiledJsonService, IGameState gameState, IDreamVM? dreamVM = null)
        {
            _typeManager = typeManager;
            _compiledJsonService = compiledJsonService;
            _gameState = gameState;
            _dreamVM = dreamVM;
        }

        public void Load(ICompiledJson compiledJson)
        {
            if (_dreamVM == null)
                return;

            _dreamVM.GameState = _gameState;
            _compiledJsonService.PopulateState(compiledJson, _dreamVM, _typeManager);
        }
    }
}
