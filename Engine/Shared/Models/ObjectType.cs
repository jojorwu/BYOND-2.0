using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shared
{
    /// <summary>
    /// Represents a type definition in the Dream environment.
    /// Contains information about variables, procedures, and inheritance.
    /// </summary>
    public class ObjectType
    {
        /// <summary>
        /// Gets the unique identifier for this type.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Gets the name of the type.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the name of the parent type.
        /// </summary>
        public string? ParentName { get; init; }

        /// <summary>
        /// Gets the parent type definition.
        /// </summary>
        [JsonIgnore]
        public ObjectType? Parent { get; set; }

        /// <summary>
        /// Gets the default property values for this type.
        /// </summary>
        public Dictionary<string, object?> DefaultProperties { get; init; } = new();

        /// <summary>
        /// Gets the names of all variables defined for this type.
        /// </summary>
        public List<string> VariableNames { get; } = new();

        /// <summary>
        /// Gets the default values for all variables, indexed by their position in <see cref="VariableNames"/>.
        /// </summary>
        public List<object?> FlattenedDefaultValues { get; } = new();

        /// <summary>
        /// Gets the procedures defined locally in this type.
        /// </summary>
        public Dictionary<string, IDreamProc> Procs { get; } = new();

        /// <summary>
        /// Gets all procedures including inherited ones.
        /// </summary>
        public Dictionary<string, IDreamProc> FlattenedProcs { get; } = new();

        private Dictionary<string, int>? _variableIndices;

        public ObjectType(int id, string name)
        {
            Id = id;
            Name = name;
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
