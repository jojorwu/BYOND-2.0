using System.Collections.Generic;
using System.Text.Json;

namespace Core
{
    public class MapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth { get; set; }
        public List<TurfData?> Turfs { get; set; } = new();
    }

    public class TurfData
    {
        public int Id { get; set; }
        public List<GameObjectData> Contents { get; set; } = new List<GameObjectData>();
    }

    public class GameObjectData
    {
        public string TypeName { get; set; } = string.Empty;
        public Dictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
