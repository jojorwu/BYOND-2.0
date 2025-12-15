using ImGuiNET;
using System.Numerics;

namespace Launcher.UI
{
    public class MainMenuPanel
    {
        public bool IsExitRequested { get; private set; }
        public bool IsEditorRequested { get; private set; }
        public bool IsServerBrowserRequested { get; private set; }

        public MainMenuPanel()
        {
        }

        public void Draw()
        {
            ImGui.StyleColorsDark(); // Use a dark theme

            // Create a full-screen, non-interactable window to serve as a background
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.Begin("MainMenuBackground",
                ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoFocusOnAppearing);
            ImGui.PopStyleVar(2);
            ImGui.End();

            // Main menu window
            ImGui.SetNextWindowPos(new Vector2(ImGui.GetIO().DisplaySize.X * 0.5f, ImGui.GetIO().DisplaySize.Y * 0.5f), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.Begin("Main Menu", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize);

            // TODO: Add logo here when available

            if (ImGui.Button("Server Browser", new Vector2(200, 40)))
            {
                IsServerBrowserRequested = true;
            }
            ImGui.Spacing();
            if (ImGui.Button("Project Editor", new Vector2(200, 40)))
            {
                IsEditorRequested = true;
            }
            ImGui.Spacing();
            if (ImGui.Button("Exit", new Vector2(200, 40)))
            {
                IsExitRequested = true;
            }

            ImGui.End();
        }
    }
}
