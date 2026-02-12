using Shared;
namespace Core.Objects
{
    using Shared.Interfaces;

    public class ObjectApi : IObjectApi
    {
        private readonly IGameState _gameState;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly IMapApi _mapApi;
        private readonly IObjectPool<GameObject> _gameObjectPool;

        public ObjectApi(IGameState gameState, IObjectTypeManager objectTypeManager, IMapApi mapApi, IObjectPool<GameObject> gameObjectPool)
        {
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapApi = mapApi;
            _gameObjectPool = gameObjectPool;
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
                var gameObject = _gameObjectPool.Rent();
                gameObject.Initialize(objectType, x, y, z);
                _gameState.AddGameObject(gameObject);
                _gameState.Map?.AddObjectToTurf(gameObject);
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
                    _gameState.Map?.RemoveObjectFromTurf(gameObject);
                    _gameState.RemoveGameObject(gameObject);
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

                    _gameState.Map?.RemoveObjectFromTurf(gameObject);

                    gameObject.X = x;
                    gameObject.Y = y;
                    gameObject.Z = z;

                    _gameState.UpdateGameObject(gameObject, oldX, oldY);
                    _gameState.Map?.AddObjectToTurf(gameObject);
                }
            }
        }
    }
}
