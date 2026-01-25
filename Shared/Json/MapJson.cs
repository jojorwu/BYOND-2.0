using System.Collections.Generic;

namespace Shared.Json
{
    public interface IMapData
    {
        IReadOnlyDictionary<string, MapCellJson> CellDefinitions { get; }
        IReadOnlyList<MapBlockJson> Blocks { get; }
        int MaxX { get; }
        int MaxY { get; }
        int MaxZ { get; }
    }

    public class MapData : IMapData
    {
        public Dictionary<string, MapCellJson> CellDefinitions { get; set; } = new();
        public List<MapBlockJson> Blocks { get; set; } = new();
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public int MaxZ { get; set; }

        IReadOnlyDictionary<string, MapCellJson> IMapData.CellDefinitions => CellDefinitions;
        IReadOnlyList<MapBlockJson> IMapData.Blocks => Blocks;
    }

    public class MapJsonObjectJson
    {
        public int Type { get; set; }
        public Dictionary<string, object>? VarOverrides { get; set; }
    }

    public class MapBlockJson
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<string> Cells { get; set; } = new();
    }

    public class MapCellJson
    {
        public MapJsonObjectJson? Turf { get; set; }
        public MapJsonObjectJson? Area { get; set; }
        public List<MapJsonObjectJson> Objects { get; set; } = new();
    }
}
