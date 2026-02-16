using Shared;

namespace Editor.History
{
    public class ChangePropertyCommand : IUndoableCommand
    {
        private readonly IGameObject _target;
        private readonly string _propertyName;
        private readonly DreamValue _oldValue;
        private readonly DreamValue _newValue;

        public string Name => $"Change {_propertyName} on {_target.ObjectType.Name}";

        public ChangePropertyCommand(IGameObject target, string propertyName, DreamValue oldValue, DreamValue newValue)
        {
            _target = target;
            _propertyName = propertyName;
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public void Execute()
        {
            if (_target is GameObject g) g.SetVariable(_propertyName, _newValue);
        }

        public void Undo()
        {
            if (_target is GameObject g) g.SetVariable(_propertyName, _oldValue);
        }
    }
}
