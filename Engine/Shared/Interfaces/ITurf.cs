using System.Collections.Generic;

namespace Shared;
    public interface ITurf
    {
        long Id { get; set; }
        IEnumerable<IGameObject> Contents { get; }
        void AddContent(IGameObject obj);
        void RemoveContent(IGameObject obj);
    }
