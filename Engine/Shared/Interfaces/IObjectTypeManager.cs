using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Shared.Interfaces
{
    public interface IObjectTypeManager
    {
        void RegisterObjectType(ObjectType objectType);
        ObjectType? GetObjectType(string name);
        ObjectType? GetObjectType(int id);
        IEnumerable<ObjectType> GetAllObjectTypes();
        ObjectType GetTurfType();
        void Clear();
    }
}
