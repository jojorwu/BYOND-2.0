using Shared;
using Shared.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Robust.Shared.Maths;

namespace Core.Maps
{
    public class DmmService : IDmmService
    {
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly IProject _project;
        private readonly IDreamMakerLoader _dreamMakerLoader;
        private readonly ILogger<DmmService> _logger;
        private readonly IDmmParserService _dmmParserService;

        public DmmService(IObjectTypeManager objectTypeManager, IProject project, IDreamMakerLoader dreamMakerLoader, ILogger<DmmService> logger, IDmmParserService dmmParserService)
        {
            _objectTypeManager = objectTypeManager;
            _project = project;
            _dreamMakerLoader = dreamMakerLoader;
            _logger = logger;
            _dmmParserService = dmmParserService;
        }

        public async Task<IMap?> LoadMapAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("DMM file not found.", filePath);
            }

            var dmFiles = _project.GetDmFiles();

            var (mapData, compiledJson) = await Task.Run(() => _dmmParserService.ParseDmm(dmFiles, filePath));

            if (mapData == null || compiledJson == null)
            {
                return null;
            }

            _dreamMakerLoader.Load(compiledJson);

            var typeIdMap = new Dictionary<int, ObjectType>();
            if (compiledJson?.Types != null) {
                typeIdMap = compiledJson.Types.Select((t, i) => new { t, i })
                    .Where(x => x.t?.Path != null && _objectTypeManager.GetObjectType(x.t.Path!) != null)
                    .ToDictionary(x => x.i, x => _objectTypeManager.GetObjectType(x.t.Path!)!);
            }

            var map = new Map();
            var chunksByZ = new Dictionary<int, Dictionary<Vector2i, Chunk>>();

            foreach (var block in mapData.Blocks)
            {
                int cellIndex = 0;
                for (int y = 0; y < block.Height; y++)
                {
                    for (int x = 0; x < block.Width; x++)
                    {
                        if (cellIndex < block.Cells.Count)
                        {
                            string cellName = block.Cells[cellIndex++];
                            if (mapData.CellDefinitions.TryGetValue(cellName, out var cellDefinition))
                            {
                                int mapX = block.X + x - 1;
                                int mapY = block.Y + (block.Height - 1 - y) - 1; // In DMM, Y is top-to-bottom
                                int mapZ = block.Z - 1;

                                // A turf's type is defined by its contents in DMM
                                var turf = new Turf(0);

                                if (cellDefinition.Turf != null)
                                {
                                    var turfObj = CreateGameObject(cellDefinition.Turf, mapX, mapY, mapZ, typeIdMap);
                                    if (turfObj != null)
                                        turf.Contents.Add(turfObj);
                                }

                                if (cellDefinition.Area != null)
                                {
                                    var areaObj = CreateGameObject(cellDefinition.Area, mapX, mapY, mapZ, typeIdMap);
                                    if (areaObj != null)
                                        turf.Contents.Add(areaObj);
                                }

                                foreach (var objJson in cellDefinition.Objects)
                                {
                                    var gameObj = CreateGameObject(objJson, mapX, mapY, mapZ, typeIdMap);
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

        private GameObject? CreateGameObject(MapJsonObjectJson mapObjectJson, int x, int y, int z, Dictionary<int, ObjectType> typeIdMap)
        {
            if (!typeIdMap.TryGetValue(mapObjectJson.Type, out var objectType))
            {
                _logger.LogWarning($"Warning: Invalid object type ID '{mapObjectJson.Type}' in DMM file.");
                return null;
            }

            var gameObject = new GameObject(objectType, x, y, z);

            if (mapObjectJson.VarOverrides != null)
            {
                foreach (var varOverride in mapObjectJson.VarOverrides)
                {
                        gameObject.SetProperty(varOverride.Key, varOverride.Value);
                }
            }
            return gameObject;
        }
    }
}
