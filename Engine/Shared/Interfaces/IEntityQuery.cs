using System.Collections.Generic;
using Shared.Models;

namespace Shared.Interfaces;

public interface IEntityQuery : IEnumerable<IGameObject>
{
    IReadOnlyList<IGameObject> Snapshot { get; }
    IEnumerable<Archetype> GetMatchingArchetypes();
    void ForEach<TVisitor>(ref TVisitor visitor) where TVisitor : struct, Archetype.IEntityVisitor, allows ref struct;
    long Version { get; }
}
