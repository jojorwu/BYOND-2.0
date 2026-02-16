using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public interface IGameStateSnapshot
    {
        IEnumerable<IGameObject> GetObjects();
        IGameObject? GetObject(int id);
    }

    public interface ISnapshotProvider
    {
        void UpdateSnapshot(IGameState state);
        IGameStateSnapshot GetCurrentSnapshot();
    }

    public class SnapshotProvider : ISnapshotProvider
    {
        private IGameStateSnapshot _currentSnapshot = new EmptySnapshot();

        public void UpdateSnapshot(IGameState state)
        {
            // Capture a point-in-time snapshot of the game objects.
            // In a more advanced implementation, this would use copy-on-write
            // or persistent data structures to minimize overhead.
            using (state.ReadLock())
            {
                var objects = state.GetAllGameObjects().ToList();
                _currentSnapshot = new StateSnapshot(objects);
            }
        }

        public IGameStateSnapshot GetCurrentSnapshot() => _currentSnapshot;

        private class EmptySnapshot : IGameStateSnapshot
        {
            public IEnumerable<IGameObject> GetObjects() => Enumerable.Empty<IGameObject>();
            public IGameObject? GetObject(int id) => null;
        }

        private class StateSnapshot : IGameStateSnapshot
        {
            private readonly Dictionary<int, IGameObject> _objects;

            public StateSnapshot(List<IGameObject> objects)
            {
                _objects = objects.ToDictionary(o => o.Id);
            }

            public IEnumerable<IGameObject> GetObjects() => _objects.Values;
            public IGameObject? GetObject(int id) => _objects.TryGetValue(id, out var obj) ? obj : null;
        }
    }
}
