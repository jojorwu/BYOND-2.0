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
