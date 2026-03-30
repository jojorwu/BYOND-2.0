using System;
using System.Collections;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Models;

public class EntityQuery : IEntityQuery
{
    private readonly IComponentQueryService _queryService;
    private readonly Type[] _componentTypes;
    private IEntityQuery? _cachedQuery;

    public EntityQuery(IComponentQueryService queryService, params Type[] componentTypes)
    {
        _queryService = queryService;
        _componentTypes = componentTypes;
    }

    private IEntityQuery Query => _cachedQuery ??= _queryService.GetQuery(_componentTypes);

    public IEnumerator<IGameObject> GetEnumerator() => Query.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IReadOnlyList<IGameObject> Snapshot => Query.Snapshot;

    public IEnumerable<Archetype> GetMatchingArchetypes() => Query.GetMatchingArchetypes();

    public void ForEach<TVisitor>(ref TVisitor visitor) where TVisitor : struct, Archetype.IEntityVisitor, allows ref struct
    {
        Query.ForEach(ref visitor);
    }

    public long Version => Query.Version;
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
