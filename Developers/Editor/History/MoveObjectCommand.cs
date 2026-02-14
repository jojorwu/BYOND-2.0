using Shared;

namespace Editor.History
{
    public class MoveObjectCommand : IUndoableCommand
    {
        private readonly IGameObject _target;
        private readonly int _oldX, _oldY, _oldZ;
        private readonly int _newX, _newY, _newZ;

        public string Name => $"Move {_target.ObjectType.Name}";

        public MoveObjectCommand(IGameObject target, int oldX, int oldY, int oldZ, int newX, int newY, int newZ)
        {
            _target = target;
            _oldX = oldX;
            _oldY = oldY;
            _oldZ = oldZ;
            _newX = newX;
            _newY = newY;
            _newZ = newZ;
        }

        public void Execute()
        {
            if (_target is GameObject g) g.SetPosition(_newX, _newY, _newZ);
        }

        public void Undo()
        {
            if (_target is GameObject g) g.SetPosition(_oldX, _oldY, _oldZ);
        }
    }
}
