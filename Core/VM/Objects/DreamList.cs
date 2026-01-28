using System.Collections.Generic;
using Shared;

namespace Core.VM.Objects
{
    public class DreamList : DreamObject
    {
        public List<DreamValue> Values { get; } = new();

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
    }
}
