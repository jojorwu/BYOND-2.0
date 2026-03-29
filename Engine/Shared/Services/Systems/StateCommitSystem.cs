using Shared.Enums;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services.Systems;

/// <summary>
/// Finalizes object state changes at the end of the tick, ensuring consistency for readers.
/// </summary>
public class StateCommitSystem : BaseSystem
{
    private readonly IGameState _gameState;

    public override string Name => "StateCommitSystem";
    public override ExecutionPhase Phase => ExecutionPhase.Cleanup;
    public override int Priority => -1000; // Run at the very end of cleanup

    public StateCommitSystem(IGameState gameState)
    {
        _gameState = gameState;
    }

    private struct CommitVisitor : IGameState.IDirtyObjectVisitor
    {
        public void Visit(IGameObject obj)
        {
            obj.CommitState();
            obj.ClearDirty();
        }
    }

    public override void Tick(IEntityCommandBuffer ecb)
    {
        var visitor = new CommitVisitor();
        _gameState.DrainDirtyObjects(ref visitor);
    }
}
