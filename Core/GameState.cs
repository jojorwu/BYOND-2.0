using System.Collections.Generic;

namespace Core
{
    public class GameState
    {
        public Map Map { get; set; }
        public List<GameObject> GameObjects { get; } = new List<GameObject>();
    }
}
