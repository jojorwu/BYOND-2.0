using System.Collections.Generic;
using Shared;
using Shared.Compiler;
using Core.Objects;
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
            for (int i = 0; i < objectTypes.Length; i++)
            {
                var type = objectTypes[i];
                type.ClearCache();
                FlattenType(type, objectTypes, jsonTypes);
            }

            for (int i = 0; i < objectTypes.Length; i++)
            {
                var type = objectTypes[i];
                FlattenProcs(type);
                type.FinalizeVariables();
            }
        }

        private void FlattenProcs(ObjectType type)
        {
            if (type.FlattenedProcs.Count > 0 || type.Name == "/") return;

            if (type.Parent != null)
            {
                FlattenProcs(type.Parent);
                foreach (var (name, proc) in type.Parent.FlattenedProcs)
                {
                    type.FlattenedProcs[name] = proc;
                }
                foreach (var (name, proc) in type.Parent.Procs)
                {
                    type.FlattenedProcs[name] = proc;
                }
            }
        }

        private void FlattenType(ObjectType type, ObjectType[] allTypes, DreamTypeJson[] jsonTypes)
        {
            if (type.VariableNames.Count > 0 || type.Name == "/") return;

            if (type.Parent == null && type.Name != "/")
            {
                var typeJson = jsonTypes[type.Id];
                if (typeJson.Parent.HasValue)
                {
                    type.Parent = allTypes[typeJson.Parent.Value];
                }
            }

            if (type.Parent != null)
            {
                FlattenType(type.Parent, allTypes, jsonTypes);
                type.VariableNames.AddRange(type.Parent.VariableNames);
                type.FlattenedDefaultValues.AddRange(type.Parent.FlattenedDefaultValues);
            }

            foreach (var (name, value) in type.DefaultProperties)
            {
                int index = type.VariableNames.IndexOf(name);
                if (index != -1)
                {
                    type.FlattenedDefaultValues[index] = value;
                }
                else
                {
                    type.VariableNames.Add(name);
                    type.FlattenedDefaultValues.Add(value);
                }
            }
        }
    }
}
