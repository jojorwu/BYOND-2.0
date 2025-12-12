using System.Collections.Generic;

namespace Shared
{
    public interface ITurf
    {
        int Id { get; set; }
        List<IGameObject> Contents { get; }
        bool IsDirty { get; set; }
    }
}
