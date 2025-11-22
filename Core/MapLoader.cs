using Newtonsoft.Json;
using System;
using System.IO;

namespace Core
{
    public static class MapLoader
    {
        public static Map LoadMap(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            var mapData = JsonConvert.DeserializeObject<MapData>(json);

            var map = new Map(mapData.Width, mapData.Height, mapData.Depth);
            int width = Math.Min(map.Width, mapData.Tiles.GetLength(0));
            int height = Math.Min(map.Height, mapData.Tiles.GetLength(1));
            int depth = Math.Min(map.Depth, mapData.Tiles.GetLength(2));

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        map.SetTile(x, y, z, new Tile(mapData.Tiles[x, y, z]));
                    }
                }
            }

            return map;
        }

        public static void SaveMap(Map map, string filePath)
        {
            var mapData = new MapData
            {
                Width = map.Width,
                Height = map.Height,
                Depth = map.Depth,
                Tiles = new int[map.Width, map.Height, map.Depth]
            };

            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    for (int z = 0; z < map.Depth; z++)
                    {
                        mapData.Tiles[x, y, z] = map.GetTile(x, y, z)?.Id ?? 0;
                    }
                }
            }

            var json = JsonConvert.SerializeObject(mapData, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private class MapData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int Depth { get; set; }
            public int[,,] Tiles { get; set; }
        }
    }
}
