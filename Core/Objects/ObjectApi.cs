using Shared;
namespace Core.Objects
{
    public class ObjectApi : IObjectApi
    {
        private readonly IGameState _gameState;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly IMapApi _mapApi;

        public ObjectApi(IGameState gameState, IObjectTypeManager objectTypeManager, IMapApi mapApi)
        {
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapApi = mapApi;
        }

        public GameObject? CreateObject(int typeId, int x, int y, int z)
        {
            var objectType = _objectTypeManager.GetObjectType(typeId);
            if (objectType == null)
            {
                return null;
            }

            using (_gameState.WriteLock())
            {
                var gameObject = new GameObject(objectType, x, y, z);
                _gameState.GameObjects.Add(gameObject.Id, gameObject);
                _gameState.SpatialGrid.Add(gameObject); // Add to grid
                var turf = _gameState.Map?.GetTurf(x, y, z);
                if (turf != null)
                {
                    turf.Contents.Add(gameObject);
                }
                return gameObject;
            }
        }

        public GameObject? GetObject(int id)
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
                    _gameState.SpatialGrid.Remove(gameObject); // Remove from grid
                    var turf = _gameState.Map?.GetTurf(gameObject.X, gameObject.Y, gameObject.Z);
                    if (turf != null)
                    {
                        turf.Contents.Remove(gameObject);
                    }
                    _gameState.GameObjects.Remove(id);
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

                    _gameState.SpatialGrid.Update(gameObject, oldX, oldY); // Update grid

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
