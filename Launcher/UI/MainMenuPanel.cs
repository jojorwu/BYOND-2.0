using ImGuiNET;
using System.Numerics;
using System;

namespace Launcher.UI
{
    public class MainMenuPanel
    {
        public bool IsExitRequested { get; private set; }
        public bool IsEditorRequested { get; set; }
        public bool IsServerBrowserRequested { get; set; }

        private bool _showErrorModal = false;
        private string _errorMessage = "";
        private readonly Texture? _logoTexture;

        public MainMenuPanel(Texture? logoTexture)
        {
            _logoTexture = logoTexture;
        }

        public void ShowError(string message)
        {
            _errorMessage = message;
            _showErrorModal = true;
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

            if (_logoTexture != null)
            {
                ImGui.Image((IntPtr)_logoTexture.Handle, new Vector2(_logoTexture.Width, _logoTexture.Height));
                ImGui.Spacing();
            }

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

            DrawErrorModal();
        }

        private void DrawErrorModal()
        {
            if (_showErrorModal)
            {
                ImGui.OpenPopup("Error");
                _showErrorModal = false; // Reset flag after opening the popup
            }

            if (ImGui.BeginPopupModal("Error", ref _showErrorModal, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text(_errorMessage);
                ImGui.Separator();
                if (ImGui.Button("OK", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }
    }
}
