using ImGuiNET;
using Shared.Config;
using System.Numerics;
using System.Linq;

namespace Client.UI
{
    public class SettingsPanel
    {
        public string Name => "Settings";
        public bool IsOpen = false;

        private readonly IConfigurationManager _manager;

        public SettingsPanel(IConfigurationManager manager)
        {
            _manager = manager;
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
