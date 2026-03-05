using System;
using System.Collections.Generic;
using ImGuiNET;

namespace Shared.Config;

public delegate void CVarUiDrawer(CVarInfo info, IConfigurationManager manager);

public static class CVarUiRegistry
{
    private static readonly Dictionary<Type, CVarUiDrawer> _drawers = new();

    static CVarUiRegistry()
    {
        RegisterDrawer(typeof(bool), (info, manager) =>
        {
            bool val = (bool)info.Value;
            if (ImGui.Checkbox(info.Name, ref val))
            {
                manager.SetCVar(info.Name, val);
            }
        });

        RegisterDrawer(typeof(int), (info, manager) =>
        {
            int val = (int)info.Value;
            if (ImGui.InputInt(info.Name, ref val))
            {
                manager.SetCVar(info.Name, val);
            }
        });

        RegisterDrawer(typeof(string), (info, manager) =>
        {
            string val = (string)info.Value;
            if (ImGui.InputText(info.Name, ref val, 256))
            {
                manager.SetCVar(info.Name, val);
            }
        });
    }

    public static void RegisterDrawer(Type type, CVarUiDrawer drawer)
    {
        _drawers[type] = drawer;
    }

    public static bool TryDraw(CVarInfo info, IConfigurationManager manager)
    {
        if (_drawers.TryGetValue(info.Type, out var drawer))
        {
            drawer(info, manager);
            return true;
        }
        return false;
    }
}
