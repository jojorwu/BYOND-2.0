using System.Collections.Generic;

namespace Shared.Interfaces;

public interface IEntityQuery : IEnumerable<IGameObject>
{
    IReadOnlyList<IGameObject> Snapshot { get; }
}
