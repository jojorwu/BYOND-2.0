using Robust.Shared.Maths;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    public class Region
    {
        public Vector2i Coords { get; }
        private readonly Dictionary<Vector2i, Chunk> _chunks;
        private readonly IScriptHost _scriptHost;

        public Region(Vector2i coords, Dictionary<Vector2i, Chunk> chunks, IScriptHost scriptHost)
        {
            Coords = coords;
            _chunks = chunks;
            _scriptHost = scriptHost;
        }

        public void Tick()
        {
            var gameObjectsInRegion = GetGameObjects();
            _scriptHost.Tick(gameObjectsInRegion, false);
        }

        public IEnumerable<Chunk> GetChunks()
        {
            return _chunks.Values;
        }

        public IEnumerable<IGameObject> GetGameObjects()
        {
            return _chunks.Values.SelectMany(chunk => chunk.GetTurfs().Where(turf => turf != null).SelectMany(turf => turf.Contents));
        }
    }
}
