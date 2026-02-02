using Shared.Models;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using ImGuiNET;
using System.Numerics;
using System;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Launcher.UI
{
    public class MainMenuPanel
    {
        public bool IsExitRequested { get; private set; }
        public bool IsEditorRequested { get; set; }
        public bool IsServerBrowserRequested { get; set; }
        public bool IsServerRequested { get; set; }
        public bool IsClientRequested { get; set; }
        public bool IsCompileRequested { get; set; }

        private bool _showErrorModal = false;
        private string _errorMessage = "";
        private readonly Texture? _logoTexture;
        private readonly IEngineManager _engineManager;
        private readonly IComputeService _computeService;

        private int _selectedTab = 0;
        private readonly List<string> _tabs = new() { "Home", "Develop", "Settings" };

        private readonly List<string> _recentProjects = new() { "Project A (maps/map.dmm)", "Awesome Game (main.dm)", "Testing Grounds" };

        private bool _checkForUpdates = true;
        private bool _sendAnalytics = false;
        private string _enginePath;

        public MainMenuPanel(Texture? logoTexture, IEngineManager engineManager, IComputeService computeService)
        {
            _logoTexture = logoTexture;
            _engineManager = engineManager;
            _computeService = computeService;
            _enginePath = _engineManager.GetBaseEnginePath();
        }

        public void ShowError(string message)
        {
            _errorMessage = message;
            _showErrorModal = true;
        }

        public void Draw()
        {
            var io = ImGui.GetIO();
            var displaySize = io.DisplaySize;

            // Background
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(displaySize);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.Begin("Background", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus);
            ImGui.PopStyleVar(2);
            ImGui.End();

            // Side bar
            float sidebarWidth = 150;
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(sidebarWidth, displaySize.Y));
            ImGui.Begin("Sidebar", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove);

            if (_logoTexture != null)
            {
                float logoWidth = sidebarWidth - 24;
                float logoHeight = (float)_logoTexture.Height / _logoTexture.Width * logoWidth;
                ImGui.SetCursorPosX(12);
                ImGui.Image((IntPtr)_logoTexture.Handle, new Vector2(logoWidth, logoHeight));
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool isSelected = _selectedTab == i;
                if (isSelected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);

                if (ImGui.Button(_tabs[i], new Vector2(sidebarWidth - 16, 40)))
                {
                    _selectedTab = i;
                }

                if (isSelected) ImGui.PopStyleColor();
                ImGui.Spacing();
            }

            ImGui.SetCursorPosY(displaySize.Y - 50);
            if (ImGui.Button("Exit", new Vector2(sidebarWidth - 16, 40)))
            {
                IsExitRequested = true;
            }

            ImGui.End();

            // Main Content Area
            ImGui.SetNextWindowPos(new Vector2(sidebarWidth, 0));
            ImGui.SetNextWindowSize(new Vector2(displaySize.X - sidebarWidth, displaySize.Y));
            ImGui.Begin("Content", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove);

            DrawTabContent();

            ImGui.End();

            DrawErrorModal();
        }

        private void DrawTabContent()
        {
            ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]); // Assumes default font for now
            ImGui.TextDisabled($"Launcher > {_tabs[_selectedTab]}");
            ImGui.Separator();
            ImGui.Spacing();

            switch (_tabs[_selectedTab])
            {
                case "Home":
                    DrawHomeTab();
                    break;
                case "Play":
                    DrawPlayTab();
                    break;
                case "Develop":
                    DrawDevelopTab();
                    break;
                case "Settings":
                    DrawSettingsTab();
                    break;
            }
        }

        private void DrawHomeTab()
        {
            ImGui.TextWrapped("Welcome to BYOND 2.0! This engine is designed for high-performance multiplayer games with a focus on deep simulation and community-driven content.");
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.2f, 0.7f, 1.0f, 1.0f), "Latest News:");
            ImGui.BeginChild("News", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None);
            ImGui.TextWrapped("- [2024-05-20] Multi-threaded Z-level processing implemented.");
            ImGui.TextWrapped("- [2024-05-18] Hot-reloading via Lua now supports complex object hierarchies.");
            ImGui.TextWrapped("- [2024-05-15] New 2.5D rendering pipeline added to the Client.");
            ImGui.EndChild();
        }

        private void DrawPlayTab()
        {
            if (ImGui.Button("Server Browser", new Vector2(250, 50)))
            {
                IsServerBrowserRequested = true;
            }
            ImGui.TextDisabled("Find and join community servers.");

            ImGui.Spacing();
            ImGui.Spacing();

            if (ImGui.Button("Start Client (Direct Connect)", new Vector2(250, 50)))
            {
                IsClientRequested = true;
            }
            ImGui.TextDisabled("Open the client to manually enter a server address.");
        }

        private void DrawDevelopTab()
        {
            ImGui.Columns(2, "DevelopColumns", true);

            // Left side: Quick Actions and Status
            ImGui.TextColored(new Vector4(0.2f, 0.7f, 1.0f, 1.0f), "Quick Actions");
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Compile & Run", new Vector2(-1, 40)))
            {
                IsCompileRequested = true;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Compiles the current project and starts a local server.");

            if (ImGui.Button("Open Editor", new Vector2(-1, 40)))
            {
                IsEditorRequested = true;
            }

            if (ImGui.Button("Launch Server", new Vector2(-1, 40)))
            {
                IsServerRequested = true;
            }

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.2f, 0.7f, 1.0f, 1.0f), "Engine Status");
            ImGui.Separator();
            DrawComponentStatus(EngineComponent.Compiler);
            DrawComponentStatus(EngineComponent.Server);
            DrawComponentStatus(EngineComponent.Editor);
            DrawComponentStatus(EngineComponent.Client);

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.2f, 0.7f, 1.0f, 1.0f), "Hardware Status");
            ImGui.Separator();
            DrawHardwareStatus();

            ImGui.NextColumn();

            // Right side: Recent Projects
            ImGui.TextColored(new Vector4(0.2f, 0.7f, 1.0f, 1.0f), "Recent Projects");
            ImGui.Separator();
            ImGui.BeginChild("RecentProjects", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), ImGuiChildFlags.None, ImGuiWindowFlags.None);
            foreach (var project in _recentProjects)
            {
                if (ImGui.Selectable(project, false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    // Logic to open project
                }
                ImGui.Separator();
            }
            ImGui.EndChild();

            if (ImGui.Button("Add Project...", new Vector2(-1, 0)))
            {
                // Action
            }

            ImGui.Columns(1);
        }

        private void DrawHardwareStatus()
        {
            ImGui.Text("Compute Device:");
            ImGui.SameLine(130);

            var device = _computeService.BestAvailableDevice;
            var color = device switch
            {
                ComputeDevice.Cuda => new Vector4(0.47f, 0.73f, 0.0f, 1.0f), // Nvidia Green
                ComputeDevice.Rocm => new Vector4(0.93f, 0.11f, 0.14f, 1.0f), // AMD Red
                ComputeDevice.Gpu => new Vector4(0.2f, 0.7f, 1.0f, 1.0f),
                _ => new Vector4(0.7f, 0.7f, 0.7f, 1.0f)
            };

            ImGui.TextColored(color, device.ToString());

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Using {device} for hardware-accelerated calculations.");
            }
        }

        private void DrawComponentStatus(EngineComponent component)
        {
            bool installed = _engineManager.IsComponentInstalled(component);
            ImGui.Text($"{component}:");
            ImGui.SameLine(100);
            if (installed)
            {
                ImGui.TextColored(new Vector4(0.3f, 0.8f, 0.3f, 1.0f), "Found");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.8f, 0.3f, 0.3f, 1.0f), "Missing");
            }
        }

        private void DrawSettingsTab()
        {
            ImGui.Text("Launcher Settings");
            ImGui.Separator();

            ImGui.Text("Engine Installation Path:");
            if (ImGui.InputText("##enginepath", ref _enginePath, 512))
            {
                _engineManager.SetBaseEnginePath(_enginePath);
            }
            ImGui.TextDisabled("This folder contains Client.exe, Server.exe, etc.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Checkbox("Check for updates on startup", ref _checkForUpdates);

            ImGui.Checkbox("Send anonymous usage data", ref _sendAnalytics);

            ImGui.Spacing();
            if (ImGui.Button("Clear Cache"))
            {
                // Action
            }
        }

        private void DrawErrorModal()
        {
            if (_showErrorModal)
            {
                ImGui.OpenPopup("Error");
                _showErrorModal = false;
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
