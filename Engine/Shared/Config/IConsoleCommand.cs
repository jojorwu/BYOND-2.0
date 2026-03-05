using System.Threading.Tasks;

namespace Shared.Config;

public interface IConsoleCommand
{
    string Command { get; }
    string Description { get; }
    string Help { get; }
    Task<string> Execute(string[] args);
}
