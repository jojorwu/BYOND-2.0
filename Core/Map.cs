namespace Core
{
    public class Map
    {
        private readonly Tile[,,] tiles;
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        public Map(int width, int height, int depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
            tiles = new Tile[width, height, depth];
        }

        public Tile GetTile(int x, int y, int z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
            {
                return null;
            }
            return tiles[x, y, z];
        }

        public void SetTile(int x, int y, int z, Tile tile)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth)
            {
                tiles[x, y, z] = tile;
            }
        }
    }
}
