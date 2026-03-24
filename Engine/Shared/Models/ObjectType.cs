using Shared.Enums;
using System.Collections.Generic;
using System.Collections.Frozen;
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
    public List<DreamValue> FlattenedDefaultValues { get; } = new();
    public DreamValue[]? DefaultValuesArray { get; private set; }
    public Dictionary<string, IDreamProc> Procs { get; } = new();
    public Dictionary<string, IDreamProc> FlattenedProcs { get; } = new();
    private Dictionary<string, int>? _variableIndices;
    private FrozenDictionary<string, int> _frozenVariableIndices = FrozenDictionary<string, int>.Empty;
    private HashSet<int>? _parentIds;
    private FrozenSet<int> _frozenParentIds = FrozenSet<int>.Empty;
    private FrozenDictionary<string, IDreamProc> _frozenProcs = FrozenDictionary<string, IDreamProc>.Empty;
    public BuiltinVar[]? VariableToBuiltin { get; private set; }
    public int XIndex = -1, YIndex = -1, ZIndex = -1, LocIndex = -1;
    public int IconIndex = -1, IconStateIndex = -1, DirIndex = -1, AlphaIndex = -1;
    public int ColorIndex = -1, LayerIndex = -1, PixelXIndex = -1, PixelYIndex = -1, OpacityIndex = -1;
    public int NameIndex = -1, DescIndex = -1;

    public ObjectType(int id, string name)
    {
        Id = id;
        Name = name;
        DefaultProperties = new Dictionary<string, object?>();
    }

    public IDreamProc? GetProc(string name)
    {
        if (_frozenProcs.TryGetValue(name, out var proc)) return proc;
        if (Procs.TryGetValue(name, out proc)) return proc;
        if (FlattenedProcs.TryGetValue(name, out proc)) return proc;
        return null;
    }

    public int GetVariableIndex(string name)
    {
        if (_frozenVariableIndices.TryGetValue(name, out int index)) return index;
        if (_variableIndices != null)
        {
            return _variableIndices.TryGetValue(name, out index) ? index : -1;
        }

        return VariableNames.IndexOf(name);
    }

    public void FinalizeVariables()
    {
        _variableIndices = new Dictionary<string, int>(VariableNames.Count);
        VariableToBuiltin = new BuiltinVar[VariableNames.Count];
        DefaultValuesArray = FlattenedDefaultValues.ToArray();
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
                "opacity" => BuiltinVar.Opacity,
                "density" => BuiltinVar.Density,
                "x" => BuiltinVar.X,
                "y" => BuiltinVar.Y,
                "z" => BuiltinVar.Z,
                "loc" => BuiltinVar.Loc,
                _ => BuiltinVar.None
            };

            switch (name)
            {
                case "x": XIndex = i; break;
                case "y": YIndex = i; break;
                case "z": ZIndex = i; break;
                case "loc": LocIndex = i; break;
                case "icon": IconIndex = i; break;
                case "icon_state": IconStateIndex = i; break;
                case "dir": DirIndex = i; break;
                case "alpha": AlphaIndex = i; break;
                case "color": ColorIndex = i; break;
                case "layer": LayerIndex = i; break;
                case "pixel_x": PixelXIndex = i; break;
                case "pixel_y": PixelYIndex = i; break;
                case "opacity": OpacityIndex = i; break;
                case "name": NameIndex = i; break;
                case "desc": DescIndex = i; break;
            }
        }

        _parentIds = new HashSet<int>();
        var current = this;
        while (current != null)
        {
            _parentIds.Add(current.Id);
            current = current.Parent;
        }
    }

    public void Freeze()
    {
        if (_variableIndices != null) _frozenVariableIndices = _variableIndices.ToFrozenDictionary();
        if (_parentIds != null) _frozenParentIds = _parentIds.ToFrozenSet();

        var allProcs = new Dictionary<string, IDreamProc>(StringComparer.Ordinal);
        foreach (var kvp in FlattenedProcs) allProcs[kvp.Key] = kvp.Value;
        foreach (var kvp in Procs) allProcs[kvp.Key] = kvp.Value;
        _frozenProcs = allProcs.ToFrozenDictionary();
    }

    public void ClearCache()
    {
        _variableIndices = null;
        _parentIds = null;
        _frozenVariableIndices = FrozenDictionary<string, int>.Empty;
        _frozenParentIds = FrozenSet<int>.Empty;
        _frozenProcs = FrozenDictionary<string, IDreamProc>.Empty;
    }

    public bool IsSubtypeOf(ObjectType other)
    {
        if (_frozenParentIds.Contains(other.Id)) return true;
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
