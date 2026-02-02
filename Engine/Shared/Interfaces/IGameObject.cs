using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Shared.Interfaces
{
    public interface IGameObject
    {
        int Id { get; }
        int X { get; set; }
        int Y { get; set; }
        int Z { get; set; }
        ObjectType ObjectType { get; }

        IEnumerable<IGameObject> Contents { get; }

        // Removed Properties dictionary in favor of DreamObject's variable system

        void SetPosition(int x, int y, int z);

        // Now using DreamValue and index-based or name-based access
        DreamValue GetVariable(string name);
        void SetVariable(string name, DreamValue value);
        DreamValue GetVariable(int index);
        void SetVariable(int index, DreamValue value);
    }
}
