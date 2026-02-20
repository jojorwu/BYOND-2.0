using Shared.Enums;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shared;

public class ObjectType
{
    public int Id { get; }
    public string Name { get; set; }
    public string? ParentName { get; set; }

    [JsonIgnore]
    public ObjectType? Parent { get; set; }
    public Dictionary<string, object?> DefaultProperties { get; set; }
    public List<string> VariableNames { get; } = new();
    public List<object?> FlattenedDefaultValues { get; } = new();
    public Dictionary<string, IDreamProc> Procs { get; } = new();
    public Dictionary<string, IDreamProc> FlattenedProcs { get; } = new();
    private Dictionary<string, int>? _variableIndices;
    private HashSet<int>? _parentIds;
    public BuiltinVar[]? VariableToBuiltin { get; private set; }

    public ObjectType(int id, string name)
    {
        Id = id;
        Name = name;
        DefaultProperties = new Dictionary<string, object?>();
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
        VariableToBuiltin = new BuiltinVar[VariableNames.Count];
        for (int i = 0; i < VariableNames.Count; i++)
        {
            var name = VariableNames[i];
            _variableIndices[name] = i;

            VariableToBuiltin[i] = name switch
            {
                "icon" => BuiltinVar.Icon,
                "icon_state" => BuiltinVar.IconState,
                "dir" => BuiltinVar.Dir,
                "alpha" => BuiltinVar.Alpha,
                "color" => BuiltinVar.Color,
                "layer" => BuiltinVar.Layer,
                "pixel_x" => BuiltinVar.PixelX,
                "pixel_y" => BuiltinVar.PixelY,
                _ => (BuiltinVar)255
            };
        }

        _parentIds = new HashSet<int>();
        var current = this;
        while (current != null)
        {
            _parentIds.Add(current.Id);
            current = current.Parent;
        }
    }

    public void ClearCache()
    {
        _variableIndices = null;
        _parentIds = null;
    }

    public bool IsSubtypeOf(ObjectType other)
    {
        if (_parentIds != null)
        {
            return _parentIds.Contains(other.Id);
        }

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
