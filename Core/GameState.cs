using System.Collections.Generic;

namespace Core
{
    public class GameState
    {
        public readonly object Lock = new();
        public Map? Map { get; set; }
        public Dictionary<int, GameObject> GameObjects { get; } = new Dictionary<int, GameObject>();
    }
}
