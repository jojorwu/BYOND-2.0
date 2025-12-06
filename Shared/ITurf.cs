using System.Collections.Generic;

namespace Core
{
    public interface ITurf
    {
        int Id { get; set; }
        List<IGameObject> Contents { get; }
    }
}
