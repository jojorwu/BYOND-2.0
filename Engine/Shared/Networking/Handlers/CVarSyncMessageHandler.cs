using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Messaging;
using Shared.Events;
using Shared.Config;

namespace Shared.Networking.Handlers;

public class CVarSyncMessageHandler : IMessageHandler
{
    private readonly IConfigurationManager _configManager;
    public byte MessageTypeId => (byte)SnapshotMessageType.SyncCVars;

    public CVarSyncMessageHandler(IConfigurationManager configManager)
    {
        _configManager = configManager;
    }

    public ValueTask HandleAsync(INetworkPeer peer, ref BitReader reader)
    {
        var msg = new CVarSyncMessage();
        msg.Read(ref reader);

        foreach(var cvar in msg.CVars)
        {
            if (_configManager is ConfigurationManager mgr) mgr.SetCVarDirect(cvar.Key, cvar.Value);
            else _configManager.SetCVar(cvar.Key, cvar.Value);
        }
        return ValueTask.CompletedTask;
    }
}
