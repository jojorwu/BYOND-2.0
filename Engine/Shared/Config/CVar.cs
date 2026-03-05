using System;

namespace Shared.Config;

/// <summary>
/// A handle to a configuration variable that provides high-performance access.
/// </summary>
public class CVar<T>
{
    private T _value;
    public string Name { get; }

    public T Value
    {
        get => _value;
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                _value = value;
                OnChanged?.Invoke(value);
            }
        }
    }

    public event Action<T>? OnChanged;

    public CVar(string name, T defaultValue)
    {
        Name = name;
        _value = defaultValue;
    }

    public static implicit operator T(CVar<T> cvar) => cvar.Value;
}
