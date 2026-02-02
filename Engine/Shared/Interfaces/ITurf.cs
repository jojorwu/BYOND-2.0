using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Shared.Interfaces
{
    public interface ITurf
    {
        int Id { get; set; }
        IEnumerable<IGameObject> Contents { get; }
        void AddContent(IGameObject obj);
        void RemoveContent(IGameObject obj);
    }
}
