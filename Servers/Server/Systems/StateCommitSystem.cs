using Shared.Attributes;
using Shared.Interfaces;
using Shared.Models;
using Shared;

namespace Server.Systems
{
    [System("StateCommitSystem", Priority = -100)] // Run last
    public class StateCommitSystem : BaseSystem
    {
        private readonly IGameState _gameState;

        public StateCommitSystem(IGameState gameState)
        {
            _gameState = gameState;
        }

        public override void Tick(IEntityCommandBuffer ecb)
        {
            _gameState.ForEachGameObject(obj => obj.CommitState());
        }
    }
}
