using Shared;

namespace Editor.History
{
    public class DeleteObjectCommand : IUndoableCommand
    {
        private readonly IGameState _gameState;
        private readonly IGameObject _target;
        private readonly int _x, _y, _z;

        public string Name => $"Delete {_target.ObjectType.Name}";

        public DeleteObjectCommand(IGameState gameState, IGameObject target)
        {
            _gameState = gameState;
            _target = target;
            _x = target.X;
            _y = target.Y;
            _z = target.Z;
        }

        public void Execute()
        {
            if (_target is GameObject g)
            {
                _gameState.Map?.GetTurf(_x, _y, _z)?.RemoveContent(g);
                _gameState.RemoveGameObject(g);
            }
        }

        public void Undo()
        {
            if (_target is GameObject g)
            {
                _gameState.Map?.GetTurf(_x, _y, _z)?.AddContent(g);
                _gameState.AddGameObject(g);
            }
        }
    }
}
