using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Models
{
    public readonly struct ComponentSignature : IEquatable<ComponentSignature>
    {
        private readonly Type[] _types;
        private readonly int _hashCode;

        public int Count => _types.Length;
        public ReadOnlySpan<Type> Types => _types;

        public ComponentSignature(IEnumerable<Type> types)
        {
            _types = types.ToArray();
            Array.Sort(_types, (a, b) => a.TypeHandle.Value.CompareTo(b.TypeHandle.Value));

            var hash = new HashCode();
            foreach (var type in _types) hash.Add(type);
            _hashCode = hash.ToHashCode();
        }

        public ComponentSignature(Type[] sortedTypes)
        {
            _types = sortedTypes;
            var hash = new HashCode();
            foreach (var type in sortedTypes) hash.Add(type);
            _hashCode = hash.ToHashCode();
        }

        public bool Equals(ComponentSignature other)
        {
            if (_hashCode != other._hashCode || _types.Length != other._types.Length) return false;
            for (int i = 0; i < _types.Length; i++)
            {
                if (_types[i] != other._types[i]) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is ComponentSignature other && Equals(other);
        public override int GetHashCode() => _hashCode;

        public bool Contains(Type type)
        {
            return Array.BinarySearch(_types, type, TypeHandleComparer.Instance) >= 0;
        }

        private class TypeHandleComparer : IComparer<Type>
        {
            public static readonly TypeHandleComparer Instance = new();
            public int Compare(Type? x, Type? y) => x!.TypeHandle.Value.CompareTo(y!.TypeHandle.Value);
        }
    }
}
