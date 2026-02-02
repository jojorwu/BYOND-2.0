using System.Collections.Generic;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Shared.Models
{
    public class DreamList : DreamObject
    {
        public List<DreamValue> Values { get; } = new();
        public Dictionary<DreamValue, DreamValue> AssociativeValues { get; } = new();
        private readonly HashSet<DreamValue> _associativeKeys = new();

        public DreamList(ObjectType listType) : base(listType)
        {
        }

        public DreamList(ObjectType listType, int size) : base(listType)
        {
            for (int i = 0; i < size; i++)
            {
                Values.Add(DreamValue.Null);
            }
        }

        public void SetValue(DreamValue key, DreamValue value)
        {
            AssociativeValues[key] = value;
            if (_associativeKeys.Add(key))
            {
                Values.Add(key);
            }
        }

        public DreamValue GetValue(DreamValue key)
        {
            if (AssociativeValues.TryGetValue(key, out var value))
            {
                return value;
            }
            return DreamValue.Null;
        }
    }
}
