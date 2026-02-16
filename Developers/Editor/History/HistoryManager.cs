using System.Collections.Generic;

namespace Editor.History
{
    public class HistoryManager
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();
        private const int MaxHistorySize = 100;

        public void ExecuteCommand(IUndoableCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();

            if (_undoStack.Count > MaxHistorySize)
            {
                // Simple way to trim the stack: we'd need a LinkedList for better performance
                // but for now this is fine.
            }
        }

        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);
            }
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
    }
}
