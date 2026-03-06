using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Config;
using Shared.Interfaces;
using Shared;
using Core;

namespace Server
{
    public class CVarReplicator : IDisposable
    {
        private readonly IConfigurationManager _configManager;
        private readonly NetDataWriterPool _writerPool;
        private readonly IPlayerManager _playerManager;

        public CVarReplicator(IConfigurationManager configManager, NetDataWriterPool writerPool, IPlayerManager playerManager)
        {
            _configManager = configManager;
            _writerPool = writerPool;
            _playerManager = playerManager;

            _configManager.OnCVarChanged += OnCVarChanged;
        }

        private void OnCVarChanged(string name, object value)
        {
            var info = _configManager.GetRegisteredCVars().FirstOrDefault(c => c.Name == name);
            if (info != null && (info.Flags & CVarFlags.Replicated) != 0)
            {
                BroadcastCVarUpdate(name, value);
            }
        }

        private void BroadcastCVarUpdate(string name, object value)
        {
            var update = new Dictionary<string, object> { { name, value } };
            var writer = _writerPool.Rent();
            try
            {
                writer.Put((byte)SnapshotMessageType.SyncCVars);
                string json = System.Text.Json.JsonSerializer.Serialize(update);
                writer.Put(json);

                var data = writer.CopyData();
                _playerManager.ForEachPlayer(peer => _ = peer.SendAsync(data));
            }
            finally
            {
                _writerPool.Return(writer);
            }
        }

        public void Dispose()
        {
            _configManager.OnCVarChanged -= OnCVarChanged;
        }
    }
}
