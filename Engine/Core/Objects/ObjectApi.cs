using Shared;
namespace Core.Api
{
    using Shared.Interfaces;

    public class ObjectApi : IObjectApi
    {
        private readonly IGameState _gameState;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly IMapApi _mapApi;
        private readonly IObjectPool<GameObject> _gameObjectPool;
        private readonly IComponentManager _componentManager;

        public ObjectApi(IGameState gameState, IObjectTypeManager objectTypeManager, IMapApi mapApi, IObjectPool<GameObject> gameObjectPool, IComponentManager componentManager)
        {
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapApi = mapApi;
            _gameObjectPool = gameObjectPool;
            _componentManager = componentManager;
        }

        public GameObject? CreateObject(int typeId, long x, long y, long z)
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

        public GameObject? GetObject(long id)
        {
            using (_gameState.ReadLock())
            {
                _gameState.GameObjects.TryGetValue(id, out var obj);
                return obj;
            }
        }

        public void DestroyObject(long id)
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

        public void MoveObject(long id, long x, long y, long z)
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

        public System.Collections.Generic.List<GameObject> FindObjectsByType(string typePath)
        {
            var results = new System.Collections.Generic.List<GameObject>();
            var targetType = _objectTypeManager.GetObjectType(typePath);
            if (targetType == null) return results;

            using (_gameState.ReadLock())
            {
                foreach (var obj in _gameState.GameObjects.Values)
                {
                    if (obj is GameObject gameObj && gameObj.ObjectType != null)
                    {
                        var current = gameObj.ObjectType;
                        while (current != null)
                        {
                            if (current == targetType)
                            {
                                results.Add(gameObj);
                                break;
                            }
                            if (current.ParentName == null) break;
                            current = _objectTypeManager.GetObjectType(current.ParentName);
                        }
                    }
                }
            }
            return results;
        }

        public void AddComponent(long objectId, string componentType)
        {
            using (_gameState.WriteLock())
            {
                if (_gameState.GameObjects.TryGetValue(objectId, out var obj))
                {
                    var component = _componentManager.CreateComponent(componentType);
                    if (component != null)
                    {
                        obj.AddComponent(component);
                    }
                }
            }
        }

        public void RemoveComponent(long objectId, string componentType)
        {
            using (_gameState.WriteLock())
            {
                if (_gameState.GameObjects.TryGetValue(objectId, out var obj))
                {
                    // Find component by type name via reflection since IComponent doesn't expose its name directly
                    var component = obj.GetComponents().FirstOrDefault(c => c.GetType().Name.Equals(componentType, System.StringComparison.OrdinalIgnoreCase));
                    if (component != null)
                    {
                        obj.RemoveComponent(component.GetType());
                    }
                }
            }
        }

        public bool HasComponent(long objectId, string componentType)
        {
            using (_gameState.ReadLock())
            {
                if (_gameState.GameObjects.TryGetValue(objectId, out var obj))
                {
                    return obj.GetComponents().Any(c => c.GetType().Name.Equals(componentType, System.StringComparison.OrdinalIgnoreCase));
                }
            }
            return false;
        }
    }
}
