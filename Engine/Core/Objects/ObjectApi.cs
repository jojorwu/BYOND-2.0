using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
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
