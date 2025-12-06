using System;
using System.Linq;
using DMCompiler.Json;
using Robust.Shared.Maths;

namespace Core
{
    public class DmmLoader
    {
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly Dictionary<int, ObjectType> _typeIdMap;

        public DmmLoader(IObjectTypeManager objectTypeManager, Dictionary<int, ObjectType> typeIdMap)
        {
            _objectTypeManager = objectTypeManager;
            _typeIdMap = typeIdMap;
        }

        public Map LoadMap(PublicDreamMapJson dreamMapJson)
        {
            var map = new Map();
            var chunksByZ = new Dictionary<int, Dictionary<Vector2i, Chunk>>();

            foreach (var block in dreamMapJson.Blocks)
            {
                int cellIndex = 0;
                for (int y = 0; y < block.Height; y++)
                {
                    for (int x = 0; x < block.Width; x++)
                    {
                        if (cellIndex < block.Cells.Count)
                        {
                            string cellName = block.Cells[cellIndex++];
                            if (dreamMapJson.CellDefinitions.TryGetValue(cellName, out var cellDefinition))
                            {
                                int mapX = block.X + x - 1;
                                int mapY = block.Y + (block.Height - 1 - y) - 1; // In DMM, Y is top-to-bottom
                                int mapZ = block.Z - 1;

                                // A turf's type is defined by its contents in DMM
                                var turf = new Turf(0);

                                if (cellDefinition.Turf != null)
                                {
                                    var turfObj = CreateGameObject(cellDefinition.Turf, mapX, mapY, mapZ);
                                    if (turfObj != null)
                                        turf.Contents.Add(turfObj);
                                }

                                if (cellDefinition.Area != null)
                                {
                                    var areaObj = CreateGameObject(cellDefinition.Area, mapX, mapY, mapZ);
                                    if (areaObj != null)
                                        turf.Contents.Add(areaObj);
                                }

                                foreach (var objJson in cellDefinition.Objects)
                                {
                                    var gameObj = CreateGameObject(objJson, mapX, mapY, mapZ);
                                    if (gameObj != null)
                                        turf.Contents.Add(gameObj);
                                }

                                var (chunkCoords, localCoords) = Map.GlobalToChunk(mapX, mapY);

                                if (!chunksByZ.TryGetValue(mapZ, out var chunks))
                                {
                                    chunks = new Dictionary<Vector2i, Chunk>();
                                    chunksByZ[mapZ] = chunks;
                                }

                                if (!chunks.TryGetValue(chunkCoords, out var chunk))
                                {
                                    chunk = new Chunk();
                                    chunks[chunkCoords] = chunk;
                                }

                                chunk.SetTurf(localCoords.X, localCoords.Y, turf);
                            }
                        }
                    }
                }
            }

            foreach (var (z, chunks) in chunksByZ)
            {
                foreach (var (chunkCoords, chunk) in chunks)
                {
                    map.SetChunk(z, chunkCoords, chunk);
                }
            }
            return map;
        }

        private GameObject? CreateGameObject(PublicMapObjectJson mapObjectJson, int x, int y, int z)
        {
            if (!_typeIdMap.TryGetValue(mapObjectJson.Type, out var objectType))
            {
                Console.WriteLine($"Warning: Invalid object type ID '{mapObjectJson.Type}' in DMM file.");
                return null;
            }

            var gameObject = new GameObject(objectType, x, y, z);

            if (mapObjectJson.VarOverrides != null)
            {
                foreach (var varOverride in mapObjectJson.VarOverrides)
                {
                    if(varOverride.Value != null)
                        gameObject.SetProperty(varOverride.Key, varOverride.Value);
                }
            }
            return gameObject;
        }
    }
}
