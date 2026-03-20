using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Models;

public readonly struct ComponentSignature : IEquatable<ComponentSignature>
{
    public readonly Type[] Types;
    public readonly ComponentMask Mask;
    private readonly int _hashCode;

    public ComponentSignature(IEnumerable<Type> types) : this(types is Type[] array ? array.AsSpan() : types.ToArray().AsSpan())
    {
    }

    public ComponentSignature(ReadOnlySpan<Type> types)
    {
        Types = types.ToArray();

        // Standard entity component sets are small (usually < 16),
        // so a simple Insertion Sort is faster than LINQ OrderBy
        for (int i = 1; i < Types.Length; i++)
        {
            var key = Types[i];
            long keyHandle = key.TypeHandle.Value.ToInt64();
            int j = i - 1;
            while (j >= 0 && Types[j].TypeHandle.Value.ToInt64() > keyHandle)
            {
                Types[j + 1] = Types[j];
                j--;
            }
            Types[j + 1] = key;
        }

        Mask = new ComponentMask();
        var hash = new HashCode();
        foreach (var type in Types)
        {
            hash.Add(type);
            Mask.Set(Services.ComponentIdRegistry.GetId(type));
        }
        _hashCode = hash.ToHashCode();
    }

    private ComponentSignature(Type[] types, ComponentMask mask, int hashCode)
    {
        Types = types;
        Mask = mask;
        _hashCode = hashCode;
    }

    public ComponentSignature With(Type type)
    {
        int componentId = Services.ComponentIdRegistry.GetId(type);
        if (Mask.Get(componentId)) return this;

        long targetHandle = type.TypeHandle.Value.ToInt64();
        int low = 0;
        int high = Types.Length - 1;
        int insertPos = Types.Length;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            long midHandle = Types[mid].TypeHandle.Value.ToInt64();
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

        var mask = Mask;
        mask.Set(componentId);

        // Fast hash update if possible, but for now we just use the mask as the primary equality component
        return new ComponentSignature(newTypes, mask, _hashCode ^ type.GetHashCode());
    }

    public ComponentSignature Without(Type type)
    {
        int componentId = Services.ComponentIdRegistry.GetId(type);
        if (!Mask.Get(componentId)) return this;

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

        var newMask = Mask;
        newMask.Unset(componentId);

        return new ComponentSignature(newTypes, newMask, _hashCode ^ type.GetHashCode());
    }

    public bool Equals(ComponentSignature other)
    {
        return Mask.Equals(other.Mask);
    }

    public override bool Equals(object? obj) => obj is ComponentSignature other && Equals(other);

    public override int GetHashCode() => _hashCode;

    public static bool operator ==(ComponentSignature left, ComponentSignature right) => left.Equals(right);
    public static bool operator !=(ComponentSignature left, ComponentSignature right) => !left.Equals(right);
}
