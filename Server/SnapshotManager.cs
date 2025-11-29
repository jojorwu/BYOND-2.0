using Core;
using LiteNetLib.Utils;

namespace Server
{
    public class SnapshotManager
    {
        private readonly GameState _gameState;

        public SnapshotManager(GameState gameState)
        {
            _gameState = gameState;
        }

        public NetDataWriter CreateSnapshot()
        {
            var writer = new NetDataWriter();
            writer.Put((byte)SnapshotMessageType.Full);

            lock (_gameState.Lock)
            {
                writer.Put(_gameState.GameObjects.Count);
                foreach (var gameObject in _gameState.GameObjects.Values)
                {
                    writer.Put(gameObject.Id);
                    writer.Put(gameObject.Position.X);
                    writer.Put(gameObject.Position.Y);

                    // Send icon path, or empty string if not specified
                    var icon = gameObject.ObjectType.GetProperty<string>("icon");
                    writer.Put(icon ?? string.Empty);
                }
            }
            return writer;
        }
    }
}
