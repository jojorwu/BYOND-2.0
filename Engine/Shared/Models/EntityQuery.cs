using System;
using System.Collections;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Models;

public class EntityQuery : IEnumerable<IGameObject>
{
    private readonly IComponentQueryService _queryService;
    private readonly Type[] _componentTypes;

    public EntityQuery(IComponentQueryService queryService, params Type[] componentTypes)
    {
        _queryService = queryService;
        _componentTypes = componentTypes;
    }

    public IEnumerator<IGameObject> GetEnumerator() => _queryService.Query(_componentTypes).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class EntityQuery<T1> : EntityQuery where T1 : class, IComponent
{
    public EntityQuery(IComponentQueryService queryService) : base(queryService, typeof(T1)) { }
}

public class EntityQuery<T1, T2> : EntityQuery
    where T1 : class, IComponent
    where T2 : class, IComponent
{
    public EntityQuery(IComponentQueryService queryService) : base(queryService, typeof(T1), typeof(T2)) { }
}

public class EntityQuery<T1, T2, T3> : EntityQuery
    where T1 : class, IComponent
    where T2 : class, IComponent
    where T3 : class, IComponent
{
    public EntityQuery(IComponentQueryService queryService) : base(queryService, typeof(T1), typeof(T2), typeof(T3)) { }
}
