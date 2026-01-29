using System.Collections.Generic;
using Shared;

namespace Core.VM.Objects
{
    public class DreamList : DreamObject
    {
        public List<DreamValue> Values { get; } = new();
        public Dictionary<DreamValue, DreamValue> AssociativeValues { get; } = new();

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
            if (!Values.Contains(key))
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
