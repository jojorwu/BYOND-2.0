using ImGuiNET;
using Shared.Config;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared;

namespace Editor.UI
{
    public class ProjectSettingsPanel : IUiPanel
    {
        public string Name => "Project Settings";
        public bool IsOpen { get; set; } = false;

        private readonly IConfigurationManager _manager;
        private List<CVarInfo>? _cachedServerCVars;
        private List<CVarInfo>? _cachedClientCVars;
        private List<CVarInfo>? _cachedAllCVars;

        public ProjectSettingsPanel(IConfigurationManager manager)
        {
            _manager = manager;
            new Shared.GlobalSettings(_manager);
            _manager.Load("project_config.json");
            _manager.OnCVarChanged += (_, _) => ClearCache();
        }

        private void ClearCache()
        {
            _cachedServerCVars = null;
            _cachedClientCVars = null;
            _cachedAllCVars = null;
        }

        public void Draw()
        {
            if (!IsOpen)
                return;

            bool isOpen = IsOpen;
            if (ImGui.Begin(Name, ref isOpen))
            {
                if (ImGui.BeginTabBar("ProjectSettingsTabs"))
                {
                    if (ImGui.BeginTabItem("Server"))
                    {
                        DrawFilteredCVars(ref _cachedServerCVars, CVarFlags.Server);
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Graphics"))
                    {
                        DrawFilteredCVars(ref _cachedClientCVars, CVarFlags.Client);
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("All"))
                    {
                        DrawFilteredCVars(ref _cachedAllCVars, CVarFlags.None);
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }

                ImGui.Separator();
                if (ImGui.Button("Save Project Configuration"))
                {
                    _manager.Save("project_config.json");
                }
                ImGui.End();
            }
            IsOpen = isOpen;
        }

        private void DrawFilteredCVars(ref List<CVarInfo>? cache, CVarFlags filter)
        {
            if (cache == null)
            {
                var query = _manager.GetRegisteredCVars();
                if (filter != CVarFlags.None)
                {
                    query = query.Where(c => (c.Flags & filter) != 0);
                }
                cache = query.OrderBy(c => c.Name).ToList();
            }

            foreach (var info in cache)
            {
                if (info.Type == typeof(bool))
                {
                    bool val = (bool)info.Value;
                    if (ImGui.Checkbox(info.Name, ref val))
                    {
                        _manager.SetCVar(info.Name, val);
                    }
                }
                else if (info.Type == typeof(int))
                {
                    int val = (int)info.Value;
                    if (ImGui.InputInt(info.Name, ref val))
                    {
                        _manager.SetCVar(info.Name, val);
                    }
                }
                else if (info.Type == typeof(string))
                {
                    string val = (string)info.Value;
                    if (ImGui.InputText(info.Name, ref val, 256))
                    {
                        _manager.SetCVar(info.Name, val);
                    }
                }
                else
                {
                    ImGui.Text($"{info.Name}: {info.Value} (Unsupported UI)");
                }

                if (!string.IsNullOrEmpty(info.Description))
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("(?)");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(info.Description);
                    }
                }
            }
        }
    }
}
