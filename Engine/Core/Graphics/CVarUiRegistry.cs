using System;
using System.Collections.Generic;
using ImGuiNET;

namespace Core.Graphics;

using Shared.Config;

public delegate void CVarUiDrawer(CVarInfo info, IConfigurationManager manager);

public static class CVarUiRegistry
{
    private static readonly Dictionary<Type, CVarUiDrawer> _drawers = new();

    static CVarUiRegistry()
    {
        RegisterDrawer(typeof(bool), (info, manager) =>
        {
            bool val = (bool)info.Value;
            ImGui.BeginDisabled(info.IsLocked);
            if (ImGui.Checkbox(info.Name, ref val))
            {
                manager.SetCVar(info.Name, val);
            }
            ImGui.EndDisabled();
        });

        RegisterDrawer(typeof(int), (info, manager) =>
        {
            int val = (int)info.Value;
            ImGui.BeginDisabled(info.IsLocked);
            if (ImGui.InputInt(info.Name, ref val))
            {
                manager.SetCVar(info.Name, val);
            }
            ImGui.EndDisabled();
        });

        RegisterDrawer(typeof(long), (info, manager) =>
        {
            long val = (long)info.Value;
            int intVal = (int)val;
            ImGui.BeginDisabled(info.IsLocked);
            if (ImGui.InputInt(info.Name, ref intVal))
            {
                manager.SetCVar(info.Name, (long)intVal);
            }
            ImGui.EndDisabled();
        });

        RegisterDrawer(typeof(double), (info, manager) =>
        {
            double val = (double)info.Value;
            ImGui.BeginDisabled(info.IsLocked);
            if (ImGui.InputDouble(info.Name, ref val))
            {
                manager.SetCVar(info.Name, val);
            }
            ImGui.EndDisabled();
        });

        RegisterDrawer(typeof(float), (info, manager) =>
        {
            float val = (float)info.Value;
            ImGui.BeginDisabled(info.IsLocked);
            if (ImGui.InputFloat(info.Name, ref val))
            {
                manager.SetCVar(info.Name, val);
            }
            ImGui.EndDisabled();
        });

        RegisterDrawer(typeof(string), (info, manager) =>
        {
            string val = (string)info.Value;
            ImGui.BeginDisabled(info.IsLocked);
            if (ImGui.InputText(info.Name, ref val, 256))
            {
                manager.SetCVar(info.Name, val);
            }
            ImGui.EndDisabled();
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
