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
        Dictionary<string, object?> Properties { get; }
        void SetPosition(int x, int y, int z);
        T? GetProperty<T>(string propertyName);
        void SetProperty(string propertyName, object? value);
        void Reset(ObjectType newType);
    }
}
