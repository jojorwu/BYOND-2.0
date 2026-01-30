// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace DMCompiler.Json
{
    public sealed class PublicDreamMapJson
    {
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public int MaxZ { get; set; }
        public Dictionary<string, PublicCellDefinitionJson> CellDefinitions { get; set; } = new();
        public List<PublicMapBlockJson> Blocks { get; set; } = new();
    }

    public sealed class PublicCellDefinitionJson
    {
        public string Name { get; set; }
        public PublicMapObjectJson? Turf { get; set; }
        public PublicMapObjectJson? Area { get; set; }
        public List<PublicMapObjectJson> Objects { get; set; } = new();

        public PublicCellDefinitionJson(string name)
        {
            Name = name;
        }
    }

    public sealed class PublicMapObjectJson
    {
        public int Type { get; set; }
        public Dictionary<string, object?>? VarOverrides { get; set; }

        public PublicMapObjectJson(int type)
        {
            Type = type;
        }
    }

    public sealed class PublicMapBlockJson
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<string> Cells { get; set; } = new();

        public PublicMapBlockJson(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
