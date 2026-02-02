using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shared.Models
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
        public Dictionary<string, IDreamProc> FlattenedProcs { get; } = new();
        private Dictionary<string, int>? _variableIndices;

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

            if (FlattenedProcs.TryGetValue(name, out proc))
            {
                return proc;
            }

            return null;
        }

        public int GetVariableIndex(string name)
        {
            if (_variableIndices != null)
            {
                return _variableIndices.TryGetValue(name, out int index) ? index : -1;
            }

            return VariableNames.IndexOf(name);
        }

        public void FinalizeVariables()
        {
            _variableIndices = new Dictionary<string, int>(VariableNames.Count);
            for (int i = 0; i < VariableNames.Count; i++)
            {
                _variableIndices[VariableNames[i]] = i;
            }
        }

        public void ClearCache()
        {
            _variableIndices = null;
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
