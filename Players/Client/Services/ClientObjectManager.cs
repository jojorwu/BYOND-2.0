using System.Collections.Generic;
using Shared;
using Shared.Interfaces;

namespace Client.Services;

public interface IClientObjectManager
{
    IDictionary<long, GameObject> World { get; }
    GameObject GetOrCreateObject(long id, int typeId);
    void RemoveObject(long id);
}

public class ClientObjectManager : IClientObjectManager
{
    private readonly IGameState _gameState;
    private readonly IObjectTypeManager _typeManager;
    private readonly IObjectFactory _objectFactory;

    public IDictionary<long, GameObject> World => _gameState.GameObjects;

    public ClientObjectManager(IGameState gameState, IObjectTypeManager typeManager, IObjectFactory objectFactory)
    {
        _gameState = gameState;
        _typeManager = typeManager;
        _objectFactory = objectFactory;
    }

    public GameObject GetOrCreateObject(long id, int typeId)
    {
        if (World.TryGetValue(id, out var obj)) return obj;

        var type = _typeManager.GetObjectType(typeId);
        if (type == null) return null!;

        var newObj = _objectFactory.Create(type, 0, 0, 0);
        newObj.Id = id;
        _gameState.AddGameObject(newObj);
        return newObj;
    }

    public void RemoveObject(long id)
    {
        if (World.TryGetValue(id, out var obj))
        {
            _gameState.RemoveGameObject(obj);
        }
    }
}
