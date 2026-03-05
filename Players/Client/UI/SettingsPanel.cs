using ImGuiNET;
using Shared.Config;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace Client.UI
{
    public class SettingsPanel
    {
        public string Name => "Settings";
        public bool IsOpen = false;

        private readonly IConfigurationManager _manager;
        private List<CVarInfo>? _cachedClientCVars;
        private List<CVarInfo>? _cachedAllCVars;

        public SettingsPanel(IConfigurationManager manager)
        {
            _manager = manager;
            _manager.OnCVarChanged += (_, _) => { _cachedClientCVars = null; _cachedAllCVars = null; };
        }

        public void Draw()
        {
            if (!IsOpen)
                return;

            ImGui.SetNextWindowSize(new Vector2(500, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin(Name, ref IsOpen))
            {
                if (ImGui.BeginTabBar("SettingsTabs"))
                {
                    if (ImGui.BeginTabItem("Client"))
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
                if (ImGui.Button("Save Configuration"))
                {
                    _manager.Save("client_config.json");
                }
                ImGui.SameLine();
                if (ImGui.Button("Close"))
                {
                    IsOpen = false;
                }
                ImGui.End();
            }
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
