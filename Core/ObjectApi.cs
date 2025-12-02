namespace Core
{
    public class ObjectApi : IObjectApi
    {
        private readonly GameState _gameState;
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly IMapApi _mapApi;

        public ObjectApi(GameState gameState, ObjectTypeManager objectTypeManager, IMapApi mapApi)
        {
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapApi = mapApi;
        }

        public GameObject? CreateObject(string typeName, int x, int y, int z)
        {
            var objectType = _objectTypeManager.GetObjectType(typeName);
            if (objectType == null)
            {
                return null;
            }

            var gameObject = new GameObject(objectType, x, y, z);
            using (_gameState.WriteLock())
            {
                _gameState.GameObjects.Add(gameObject.Id, gameObject);
                var turf = _mapApi.GetTurf(x, y, z);
                if (turf != null)
                {
                    turf.Contents.Add(gameObject);
                }
            }
            return gameObject;
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
                var gameObject = GetObject(id);
                if (gameObject != null)
                {
                    var turf = _mapApi.GetTurf(gameObject.X, gameObject.Y, gameObject.Z);
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
                var gameObject = GetObject(id);
                if (gameObject != null)
                {
                    var oldTurf = _mapApi.GetTurf(gameObject.X, gameObject.Y, gameObject.Z);
                    if (oldTurf != null)
                    {
                        oldTurf.Contents.Remove(gameObject);
                    }

                    gameObject.X = x;
                    gameObject.Y = y;
                    gameObject.Z = z;

                    var newTurf = _mapApi.GetTurf(x, y, z);
                    if (newTurf != null)
                    {
                        newTurf.Contents.Add(gameObject);
                    }
                }
            }
        }
    }
}
