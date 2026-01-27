using System.Collections.Generic;

namespace Shared
{
    public interface IGameObject
    {
        int Id { get; }
        int X { get; set; }
        int Y { get; set; }
        int Z { get; set; }
        ObjectType ObjectType { get; }

        // Removed Properties dictionary in favor of DreamObject's variable system

        void SetPosition(int x, int y, int z);

        // Now using DreamValue and index-based or name-based access
        DreamValue GetVariable(string name);
        void SetVariable(string name, DreamValue value);
        DreamValue GetVariable(int index);
        void SetVariable(int index, DreamValue value);
    }
}
