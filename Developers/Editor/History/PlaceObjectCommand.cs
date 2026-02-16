using Shared;

namespace Editor.History
{
    public class PlaceObjectCommand : IUndoableCommand
    {
        private readonly IGameState _gameState;
        private readonly ObjectType _type;
        private readonly int _x, _y, _z;
        private GameObject? _createdObject;

        public string Name => $"Place {_type.Name}";

        public PlaceObjectCommand(IGameState gameState, ObjectType type, int x, int y, int z)
        {
            _gameState = gameState;
            _type = type;
            _x = x;
            _y = y;
            _z = z;
        }

        public void Execute()
        {
            if (_createdObject == null)
            {
                _createdObject = new GameObject(_type, _x, _y, _z);
            }
            _gameState.Map?.GetTurf(_x, _y, _z)?.AddContent(_createdObject);
            _gameState.AddGameObject(_createdObject);
        }

        public void Undo()
        {
            if (_createdObject != null)
            {
                _gameState.Map?.GetTurf(_x, _y, _z)?.RemoveContent(_createdObject);
                _gameState.RemoveGameObject(_createdObject);
            }
        }
    }
}
