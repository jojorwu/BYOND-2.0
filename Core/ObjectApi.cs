using Shared;
using System.Threading;

namespace Core
{
    public class ObjectApi : IObjectApi
    {
        private static int _nextId = 1;
        private readonly IGameState _gameState;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly IMapApi _mapApi;
        private readonly ServerSettings _settings;
        private readonly ObjectPool<GameObject> _gameObjectPool;

        public ObjectApi(IGameState gameState, IObjectTypeManager objectTypeManager, IMapApi mapApi, ServerSettings settings)
        {
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapApi = mapApi;
            _settings = settings;
            _gameObjectPool = new ObjectPool<GameObject>(() => {
                var rootType = _objectTypeManager.GetObjectType("/obj");
                if (rootType == null)
                    throw new System.InvalidOperationException("Root object type '/obj' not found.");
                return new GameObject(rootType);
            });
        }

        public IGameObject? CreateObject(int typeId, int x, int y, int z)
        {
            using (_gameState.ReadLock())
            {
                if (_gameState.GameObjects.Count >= _settings.MaxObjects)
                {
                    System.Console.WriteLine($"[Warning] Object limit ({_settings.MaxObjects}) reached. Cannot create new object of typeId {typeId}.");
                    return null;
                }
            }

            var objectType = _objectTypeManager.GetObjectType(typeId);
            if (objectType == null)
            {
                return null;
            }

            var gameObject = _gameObjectPool.Get();
            gameObject.Reset(objectType);
            gameObject.Id = Interlocked.Increment(ref _nextId);
            gameObject.SetPosition(x, y, z);

            using (_gameState.WriteLock())
            {
                _gameState.GameObjects.Add(gameObject.Id, gameObject);
                _gameState.SpatialGrid.Add(gameObject);
                var turf = _gameState.Map?.GetTurf(x, y, z);
                if (turf != null)
                {
                    turf.Contents.Add(gameObject);
                }
                return gameObject;
            }
        }

        public IGameObject? GetObject(int id)
        {
            using (_gameState.ReadLock())
            {
                _gameState.GameObjects.TryGetValue(id, out var obj);
                return obj;
            }
        }

        public void DestroyObject(int id)
        {
            using (_gameState.WriteLock())
            {
                if (_gameState.GameObjects.TryGetValue(id, out var gameObject))
                {
                    _gameState.SpatialGrid.Remove(gameObject);
                    var turf = _gameState.Map?.GetTurf(gameObject.X, gameObject.Y, gameObject.Z);
                    if (turf != null)
                    {
                        turf.Contents.Remove(gameObject);
                    }
                    _gameState.GameObjects.Remove(id);
                    _gameObjectPool.Return(gameObject);
                }
            }
        }

        public void MoveObject(int id, int x, int y, int z)
        {
            using (_gameState.WriteLock())
            {
                if (_gameState.GameObjects.TryGetValue(id, out var gameObject))
                {
                    var oldX = gameObject.X;
                    var oldY = gameObject.Y;
                    var oldTurf = _gameState.Map?.GetTurf(oldX, oldY, gameObject.Z);
                    if (oldTurf != null)
                    {
                        oldTurf.Contents.Remove(gameObject);
                    }

                    gameObject.X = x;
                    gameObject.Y = y;
                    gameObject.Z = z;
                    _gameState.SpatialGrid.Update(gameObject, oldX, oldY);

                    var newTurf = _gameState.Map?.GetTurf(x, y, z);
                    if (newTurf != null)
                    {
                        newTurf.Contents.Add(gameObject);
                    }
                }
            }
        }
    }
}
