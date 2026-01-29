using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shared
{
    public class ObjectType
    {
        public int Id { get; }
        public string Name { get; set; }
        public string? ParentName { get; set; }

        [JsonIgnore]
        public ObjectType? Parent { get; set; }
        public Dictionary<string, object?> DefaultProperties { get; set; }
        public List<string> VariableNames { get; } = new();
        public List<object?> FlattenedDefaultValues { get; } = new();
        public Dictionary<string, IDreamProc> Procs { get; } = new();
        private readonly Dictionary<string, int> _variableIndexCache = new();

        public ObjectType(int id, string name)
        {
            Id = id;
            Name = name;
            DefaultProperties = new Dictionary<string, object?>();
        }

        public IDreamProc? GetProc(string name)
        {
            if (Procs.TryGetValue(name, out var proc))
            {
                return proc;
            }

            return Parent?.GetProc(name);
        }

        public int GetVariableIndex(string name)
        {
            if (_variableIndexCache.TryGetValue(name, out int index))
            {
                return index;
            }

            index = VariableNames.IndexOf(name);
            if (index != -1)
            {
                _variableIndexCache[name] = index;
            }

            return index;
        }

        public void ClearCache()
        {
            _variableIndexCache.Clear();
        }

        public bool IsSubtypeOf(ObjectType other)
        {
            var current = this;
            while (current != null)
            {
                if (current == other)
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }
    }
}
