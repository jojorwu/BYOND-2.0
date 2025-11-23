
using ImGuiNET;
using Core;
using System;
using System.Numerics;

namespace Editor.UI
{
    public enum SettingsAction
    {
        None,
        Back
    }

    public class SettingsPanel
    {
        private readonly EngineSettings _engineSettings;

        public SettingsPanel(EngineSettings engineSettings)
        {
            _engineSettings = engineSettings;
        }

        public SettingsAction Draw()
        {
            var action = SettingsAction.None;

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.Begin("Engine Settings", ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.Text("Multi-threading Settings");
            ImGui.Separator();

            bool enableMultiThreading = _engineSettings.EnableMultiThreading;
            if (ImGui.Checkbox("Enable Multi-threading", ref enableMultiThreading))
            {
                _engineSettings.EnableMultiThreading = enableMultiThreading;
            }

            if (enableMultiThreading)
            {
                bool isAuto = _engineSettings.NumberOfThreads == 0;
                if (ImGui.RadioButton("Automatic", isAuto))
                {
                    _engineSettings.NumberOfThreads = 0;
                }
                if (ImGui.RadioButton("Manual", !isAuto))
                {
                    if (isAuto)
                    {
                        _engineSettings.NumberOfThreads = Environment.ProcessorCount;
                    }
                }
                if (!isAuto)
                {
                    int numThreads = _engineSettings.NumberOfThreads;
                    if (ImGui.InputInt("Number of Threads", ref numThreads, 1, 1))
                    {
                        _engineSettings.NumberOfThreads = Math.Max(1, numThreads);
                    }
                }
                ImGui.Text($"Effective number of threads: {_engineSettings.EffectiveNumberOfThreads}");
            }

            ImGui.Separator();
            if (ImGui.Button("Save Settings", new Vector2(120, 0)))
            {
                _engineSettings.Save();
            }
            if (ImGui.Button("Back", new Vector2(120, 0)))
            {
                action = SettingsAction.Back;
            }
            ImGui.End();

            return action;
        }
    }
}
