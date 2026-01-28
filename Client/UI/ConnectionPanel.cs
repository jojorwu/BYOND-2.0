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
            ImGui.StyleColorsDark();

            ImGui.SetNextWindowPos(new Vector2(ImGui.GetIO().DisplaySize.X * 0.5f, ImGui.GetIO().DisplaySize.Y * 0.5f), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.Begin("Connect to Server", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.InputText("Server Address", ref ServerAddress, 256);
            ImGui.Spacing();

            if (ImGui.Button("Connect", new Vector2(200, 40)))
            {
                IsConnectRequested = true;
            }

            ImGui.End();
        }
    }
}
