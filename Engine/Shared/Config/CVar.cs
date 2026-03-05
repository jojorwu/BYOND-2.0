using System;

using System;
using System.Collections.Generic;

namespace Shared.Config;

public interface ICVar
{
    string Name { get; }
    object Value { get; set; }
    Type Type { get; }
}

/// <summary>
/// A handle to a configuration variable that provides high-performance access.
/// </summary>
public class CVar<T> : ICVar
{
    private volatile object _value;
    public string Name { get; }
    public Type Type => typeof(T);

    public T Value
    {
        get => (T)_value;
        set
        {
            if (!EqualityComparer<T>.Default.Equals((T)_value, value))
            {
                _value = value!;
                OnChanged?.Invoke(value);
            }
        }
    }

    object ICVar.Value
    {
        get => Value!;
        set => Value = (T)value;
    }

    public event Action<T>? OnChanged;

    public CVar(string name, T defaultValue)
    {
        Name = name;
        _value = defaultValue!;
    }

    public static implicit operator T(CVar<T> cvar) => cvar.Value;
}
