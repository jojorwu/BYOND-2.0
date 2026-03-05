using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Config;

public class CVarListCommand : IConsoleCommand
{
    private readonly IConfigurationManager _manager;
    public string Command => "cvar_list";
    public string Description => "Lists all registered configuration variables.";
    public string Help => "Usage: cvar_list [filter]";

    public CVarListCommand(IConfigurationManager manager) => _manager = manager;

    public Task<string> Execute(string[] args)
    {
        var filter = args.Length > 0 ? args[0].ToLowerInvariant() : null;
        var cvars = _manager.GetRegisteredCVars()
            .Where(c => filter == null || c.Name.ToLowerInvariant().Contains(filter))
            .OrderBy(c => c.Name);

        var sb = new StringBuilder();
        sb.AppendLine("Registered CVars:");
        foreach (var c in cvars)
        {
            sb.AppendLine($"  {c.Name} = {c.Value} ({c.Type.Name})");
        }
        return Task.FromResult(sb.ToString());
    }
}

public class CVarSetCommand : IConsoleCommand
{
    private readonly IConfigurationManager _manager;
    public string Command => "cvar_set";
    public string Description => "Sets the value of a configuration variable.";
    public string Help => "Usage: cvar_set <name> <value>";

    public CVarSetCommand(IConfigurationManager manager) => _manager = manager;

    public Task<string> Execute(string[] args)
    {
        if (args.Length < 2) return Task.FromResult(Help);
        var name = args[0];
        var value = args[1];

        try
        {
            if (_manager is ConfigurationManager mgr) mgr.SetCVarDirect(name, value);
            else _manager.SetCVar(name, (object)value);
            return Task.FromResult($"CVar '{name}' set to '{value}'.");
        }
        catch (System.Exception ex)
        {
            return Task.FromResult($"Failed to set CVar '{name}': {ex.Message}");
        }
    }
}
