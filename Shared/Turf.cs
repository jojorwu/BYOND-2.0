using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Represents a tile on the game map.
    /// </summary>
    public class Turf : GameObject, ITurf
    {
        private readonly object _contentsLock = new();
        private readonly List<IGameObject> _contents = new();

        /// <summary>
        /// Gets the list of game objects currently on this turf.
        /// </summary>
        public IEnumerable<IGameObject> Contents
        {
            get
            {
                lock (_contentsLock)
                {
                    return new List<IGameObject>(_contents);
                }
            }
        }

        public void AddContent(IGameObject obj)
        {
            lock (_contentsLock)
            {
                if (!_contents.Contains(obj))
                    _contents.Add(obj);
            }
        }

        public void RemoveContent(IGameObject obj)
        {
            lock (_contentsLock)
            {
                _contents.Remove(obj);
            }
        }

        public Turf(ObjectType objectType, int x, int y, int z) : base(objectType, x, y, z)
        {
        }
    }
}
