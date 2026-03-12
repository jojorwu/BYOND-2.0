using System.Threading.Tasks;

namespace Shared.Interfaces;

/// <summary>
/// A command that supports undoing its actions.
/// </summary>
public interface IReversibleCommand : ICommand
{
    Task UndoAsync();
}
