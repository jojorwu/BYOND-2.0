using Shared.Attributes;
using Shared.Interfaces;
using Shared.Models;
using Shared;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Server.Systems
{
    [System("ScriptSystem", Priority = 50)]
    [Resource(typeof(IScriptHost), ResourceAccess.Write)]
    public class ScriptSystem : BaseSystem
    {
        private readonly IScriptHost _scriptHost;
        private readonly ServerSettings _settings;

        public ScriptSystem(IScriptHost scriptHost, IOptions<ServerSettings> settings)
        {
            _scriptHost = scriptHost;
            _settings = settings.Value;
        }

        public override bool Enabled => !_settings.Performance.EnableRegionalProcessing;

        public override void Tick(IEntityCommandBuffer ecb)
        {
            _scriptHost.TickAsync().GetAwaiter().GetResult();
        }
    }
}
