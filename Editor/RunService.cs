using System;
using System.IO;
using ImGuiNET;
using Launcher;

namespace Editor
{
    public class RunService : IRunService
    {
        private readonly IProcessService _processService;
        private readonly IEditorSettingsManager _settingsManager;
        private string _error = string.Empty;

        public RunService(IProcessService processService, IEditorSettingsManager settingsManager)
        {
            _processService = processService;
            _settingsManager = settingsManager;
        }

        public void Run()
        {
            var serverExecutable = _settingsManager.Settings.ServerExecutablePath;
            var clientExecutable = _settingsManager.Settings.ClientExecutablePath;

            var serverPath = Path.Combine(AppContext.BaseDirectory, serverExecutable);
            var clientPath = Path.Combine(AppContext.BaseDirectory, clientExecutable);

            try
            {
                _processService.Start(serverPath);
                _processService.Start(clientPath);
            }
            catch (Exception e)
            {
                _error = $"Failed to run project: {e.Message}";
            }
        }

        public void Draw()
        {
            if (!string.IsNullOrEmpty(_error))
            {
                ImGui.OpenPopup("Error");
            }

            bool isOpen = true;
            if (ImGui.BeginPopupModal("Error", ref isOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text(_error);
                if (ImGui.Button("OK"))
                {
                    _error = string.Empty;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }
    }
}
