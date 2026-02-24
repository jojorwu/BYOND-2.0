using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Models;

public readonly struct ComponentSignature : IEquatable<ComponentSignature>
{
    public readonly Type[] Types;
    private readonly int _hashCode;

    public ComponentSignature(IEnumerable<Type> types)
    {
        Types = types.OrderBy(t => t.TypeHandle.Value.ToInt64()).ToArray();

        var hash = new HashCode();
        foreach (var type in Types)
        {
            hash.Add(type);
        }
        _hashCode = hash.ToHashCode();
    }

    private ComponentSignature(Type[] types, int hashCode)
    {
        Types = types;
        _hashCode = hashCode;
    }

    public ComponentSignature With(Type type)
    {
        if (Types.Contains(type)) return this;

        var newTypes = new Type[Types.Length + 1];
        int i = 0;
        bool inserted = false;
        long targetHandle = type.TypeHandle.Value.ToInt64();

        foreach (var existing in Types)
        {
            if (!inserted && existing.TypeHandle.Value.ToInt64() > targetHandle)
            {
                newTypes[i++] = type;
                inserted = true;
            }
            newTypes[i++] = existing;
        }

        if (!inserted) newTypes[Types.Length] = type;

        var hash = new HashCode();
        foreach (var t in newTypes) hash.Add(t);
        return new ComponentSignature(newTypes, hash.ToHashCode());
    }

    public ComponentSignature Without(Type type)
    {
        if (!Types.Contains(type)) return this;

        var newTypes = new Type[Types.Length - 1];
        int i = 0;
        foreach (var existing in Types)
        {
            if (existing == type) continue;
            newTypes[i++] = existing;
        }

        var hash = new HashCode();
        foreach (var t in newTypes) hash.Add(t);
        return new ComponentSignature(newTypes, hash.ToHashCode());
    }

    public bool Equals(ComponentSignature other)
    {
        if (Types.Length != other.Types.Length) return false;
        for (int i = 0; i < Types.Length; i++)
        {
            if (Types[i] != other.Types[i]) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is ComponentSignature other && Equals(other);

    public override int GetHashCode() => _hashCode;

    public static bool operator ==(ComponentSignature left, ComponentSignature right) => left.Equals(right);
    public static bool operator !=(ComponentSignature left, ComponentSignature right) => !left.Equals(right);
}
