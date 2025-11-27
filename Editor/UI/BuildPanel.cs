using ImGuiNET;
using System.Numerics;

namespace Editor.UI
{
    public class BuildPanel
    {
        private readonly BuildService _buildService;

        public BuildPanel(BuildService buildService)
        {
            _buildService = buildService;
        }

        public void Draw()
        {
            ImGui.Begin("Build Output");
            if (ImGui.BeginTable("build_messages", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Level");
                ImGui.TableSetupColumn("File");
                ImGui.TableSetupColumn("Line");
                ImGui.TableSetupColumn("Message");
                ImGui.TableHeadersRow();

                foreach (var message in _buildService.Messages)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var color = message.Level switch
                    {
                        BuildMessageLevel.Error => new Vector4(1, 0, 0, 1),
                        BuildMessageLevel.Warning => new Vector4(1, 1, 0, 1),
                        _ => new Vector4(1, 1, 1, 1)
                    };
                    ImGui.TextColored(color, message.Level.ToString());
                    ImGui.TableNextColumn();
                    ImGui.Text(message.File);
                    ImGui.TableNextColumn();
                    if (message.Line > 0)
                        ImGui.Text(message.Line.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(message.Text);
                }
                ImGui.EndTable();
            }
            ImGui.End();
        }
    }
}
