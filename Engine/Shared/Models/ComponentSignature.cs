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
        long targetHandle = type.TypeHandle.Value.ToInt64();
        int low = 0;
        int high = Types.Length - 1;
        int insertPos = Types.Length;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            long midHandle = Types[mid].TypeHandle.Value.ToInt64();
            if (midHandle == targetHandle) return this;
            if (midHandle > targetHandle)
            {
                insertPos = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        var newTypes = new Type[Types.Length + 1];
        if (insertPos > 0) Array.Copy(Types, 0, newTypes, 0, insertPos);
        newTypes[insertPos] = type;
        if (insertPos < Types.Length) Array.Copy(Types, insertPos, newTypes, insertPos + 1, Types.Length - insertPos);

        var hash = new HashCode();
        foreach (var t in newTypes) hash.Add(t);
        return new ComponentSignature(newTypes, hash.ToHashCode());
    }

    public ComponentSignature Without(Type type)
    {
        long targetHandle = type.TypeHandle.Value.ToInt64();
        int low = 0, high = Types.Length - 1;
        int removeIdx = -1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            long midHandle = Types[mid].TypeHandle.Value.ToInt64();
            if (midHandle == targetHandle)
            {
                removeIdx = mid;
                break;
            }
            if (midHandle > targetHandle) high = mid - 1;
            else low = mid + 1;
        }

        if (removeIdx == -1) return this;

        var newTypes = new Type[Types.Length - 1];
        if (removeIdx > 0) Array.Copy(Types, 0, newTypes, 0, removeIdx);
        if (removeIdx < Types.Length - 1) Array.Copy(Types, removeIdx + 1, newTypes, removeIdx, Types.Length - removeIdx - 1);

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
