using ImGuiNET;
using System.Numerics;

namespace Client.UI
{
    public class ConnectionPanel
    {
        public string ServerAddress = "127.0.0.1:12345";
        public bool IsConnectRequested { get; set; }

        public void Draw()
        {
            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(400, 0));

            if (ImGui.Begin("Connect to Server", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 10));

                ImGui.SetWindowFontScale(1.5f);
                ImGui.TextColored(new Vector4(0.2f, 0.4f, 0.8f, 1.0f), "BYOND 2.0");
                ImGui.SetWindowFontScale(1.0f);
                ImGui.TextDisabled("Modern Engine for Modern Dreams");
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("Server Address:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##ServerAddress", ref ServerAddress, 256);

                ImGui.Spacing();
                ImGui.Spacing();

                if (ImGui.Button("Connect", new Vector2(-1, 45)))
                {
                    IsConnectRequested = true;
                }

                ImGui.PopStyleVar();
            }
            ImGui.End();
        }
    }
}
