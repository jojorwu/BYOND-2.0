using System.Collections.Generic;
using Shared;
using Shared.Compiler;
using Core.Utils;

namespace Core
{
    public interface ITypeSystemPopulator
    {
        void PopulateTypes(DreamTypeJson[] jsonTypes, ObjectType[] objectTypes);
    }

    public class TypeSystemPopulator : ITypeSystemPopulator
    {
        public void PopulateTypes(DreamTypeJson[] jsonTypes, ObjectType[] objectTypes)
        {
            // Reset and link parents first
            for (int i = 0; i < objectTypes.Length; i++)
            {
                var type = objectTypes[i];
                type.ClearCache();
                if (type.Parent == null && type.Name != "/")
                {
                    var typeJson = jsonTypes[type.Id];
                    if (typeJson.Parent.HasValue)
                    {
                        type.Parent = objectTypes[typeJson.Parent.Value];
                    }
                }
            }

            // Topological-ish processing using a simple work queue / dependency counter
            // to ensure parent types are always flattened before children.
            var pending = new Queue<ObjectType>();
            var childCount = new Dictionary<ObjectType, int>();
            var children = new Dictionary<ObjectType, List<ObjectType>>();

            foreach (var type in objectTypes)
            {
                if (type.Parent == null)
                {
                    pending.Enqueue(type);
                }
                else
                {
                    if (!children.TryGetValue(type.Parent, out var list))
                    {
                        list = new List<ObjectType>();
                        children[type.Parent] = list;
                    }
                    list.Add(type);
                    childCount[type] = 1; // Basic inheritance is 1-to-1 parent-child in DM
                }
            }

            while (pending.Count > 0)
            {
                var type = pending.Dequeue();

                // Flattening Logic
                FlattenTypeIterative(type);
                FlattenProcsIterative(type);

                if (children.TryGetValue(type, out var childList))
                {
                    foreach (var child in childList)
                    {
                        pending.Enqueue(child);
                    }
                }
            }

            // Finalization phase
            for (int i = 0; i < objectTypes.Length; i++)
            {
                objectTypes[i].FinalizeVariables();
            }
        }

        private void FlattenProcsIterative(ObjectType type)
        {
            if (type.Name != "/" && type.Parent != null)
            {
                // Pre-size to avoid reallocations
                int capacity = type.Parent.FlattenedProcs.Count + type.Procs.Count;
                if (type.FlattenedProcs.Count < capacity)
                {
                    // Dictionary doesn't have a direct resize, but we can copy
                    foreach (var kvp in type.Parent.FlattenedProcs)
                    {
                        type.FlattenedProcs[kvp.Key] = kvp.Value;
                    }
                }
            }

            foreach (var kvp in type.Procs)
            {
                type.FlattenedProcs[kvp.Key] = kvp.Value;
            }
        }

        private void FlattenTypeIterative(ObjectType type)
        {
            if (type.Name == "/") return;

            if (type.Parent != null)
            {
                // Pre-size lists
                type.VariableNames.EnsureCapacity(type.Parent.VariableNames.Count + type.DefaultProperties.Count);
                type.FlattenedDefaultValues.EnsureCapacity(type.Parent.FlattenedDefaultValues.Count + type.DefaultProperties.Count);

                type.VariableNames.AddRange(type.Parent.VariableNames);
                type.FlattenedDefaultValues.AddRange(type.Parent.FlattenedDefaultValues);
            }

            foreach (var (name, value) in type.DefaultProperties)
            {
                var dreamValue = DreamValue.FromObject(value);
                int index = type.VariableNames.IndexOf(name);
                if (index != -1)
                {
                    type.FlattenedDefaultValues[index] = dreamValue;
                }
                else
                {
                    type.VariableNames.Add(name);
                    type.FlattenedDefaultValues.Add(dreamValue);
                }
            }
        }
    }
}
