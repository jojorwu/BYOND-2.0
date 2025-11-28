using System;
using System.Linq;
using DMCompiler.Json;

namespace Core
{
    public class DmmLoader
    {
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly Dictionary<int, ObjectType> _typeIdMap;

        public DmmLoader(ObjectTypeManager objectTypeManager, Dictionary<int, ObjectType> typeIdMap)
        {
            _objectTypeManager = objectTypeManager;
            _typeIdMap = typeIdMap;
        }

        public Map LoadMap(PublicDreamMapJson dreamMapJson)
        {
            var map = new Map(dreamMapJson.MaxX, dreamMapJson.MaxY, dreamMapJson.MaxZ);

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

                                if (mapX >= 0 && mapX < map.Width &&
                                    mapY >= 0 && mapY < map.Height &&
                                    mapZ >= 0 && mapZ < map.Depth)
                                {
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
                                    map.SetTurf(mapX, mapY, mapZ, turf);
                                }
                            }
                        }
                    }
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
