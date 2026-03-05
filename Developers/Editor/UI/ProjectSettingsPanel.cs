using ImGuiNET;
using Shared.Config;
using System.Numerics;
using System.Linq;
using Shared.Interfaces;
using Shared;

namespace Editor.UI
{
    public class ProjectSettingsPanel : IUiPanel
    {
        public string Name => "Project Settings";
        public bool IsOpen { get; set; } = false;

        private readonly IConfigurationManager _manager;

        public ProjectSettingsPanel(IConfigurationManager manager)
        {
            _manager = manager;
            new Shared.GlobalSettings(_manager);
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
                        DrawFilteredCVars(CVarFlags.Server);
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Graphics"))
                    {
                        DrawFilteredCVars(CVarFlags.Client);
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("All"))
                    {
                        DrawFilteredCVars(CVarFlags.None);
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

        private void DrawFilteredCVars(CVarFlags filter)
        {
            var cvars = _manager.GetRegisteredCVars();
            if (filter != CVarFlags.None)
            {
                cvars = cvars.Where(c => (c.Flags & filter) != 0);
            }

            foreach (var info in cvars.OrderBy(c => c.Name))
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
