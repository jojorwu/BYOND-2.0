using Shared;

namespace Editor.History
{
    public class MoveObjectCommand : IUndoableCommand
    {
        private readonly IGameObject _target;
        private readonly long _oldX, _oldY, _oldZ;
        private readonly long _newX, _newY, _newZ;

        public string Name => $"Move {_target.ObjectType.Name}";

        public MoveObjectCommand(IGameObject target, long oldX, long oldY, long oldZ, long newX, long newY, long newZ)
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
