using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core
{
    public class GameState
    {
        public Map? Map { get; set; }
        public Dictionary<int, GameObject> GameObjects { get; } = new Dictionary<int, GameObject>();

        public string GetSnapshot()
        {
            var snapshot = new
            {
                Map,
                GameObjects
            };
            return JsonConvert.SerializeObject(snapshot);
        }
    }
}
