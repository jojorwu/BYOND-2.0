using System.Threading.Tasks;

namespace Shared.Interfaces
{
    /// <summary>
    /// Represents a game action that can be executed and potentially undone.
    /// </summary>
    public interface ICommand
    {
        string Name { get; }
        Task ExecuteAsync();
    }
}
