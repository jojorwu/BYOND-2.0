using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class ScriptEditorPanel
    {
        private readonly GameApi _gameApi;
        private string[] _scriptFiles = System.Array.Empty<string>();
        private string? _selectedScript;
        private string _scriptContent = string.Empty;

        public ScriptEditorPanel(GameApi gameApi)
        {
            _gameApi = gameApi;
            _scriptFiles = _gameApi.ListScriptFiles().ToArray();
        }

        public void Draw()
        {
            ImGui.Begin("Script Editor");
            ImGui.Columns(2, "ScriptEditorColumns", true);

            ImGui.BeginChild("ScriptFiles");
            foreach (var scriptFile in _scriptFiles)
            {
                if (ImGui.Selectable(scriptFile, _selectedScript == scriptFile))
                {
                    _selectedScript = scriptFile;
                    _scriptContent = _gameApi.ReadScriptFile(scriptFile);
                }
            }
            ImGui.EndChild();

            ImGui.NextColumn();

            ImGui.BeginChild("ScriptContent");
            if (ImGui.Button("Save") && _selectedScript != null)
            {
                _gameApi.WriteScriptFile(_selectedScript, _scriptContent);
            }
            ImGui.SameLine();
            ImGui.Text(_selectedScript ?? "No script selected");
            ImGui.InputTextMultiline("##ScriptContent", ref _scriptContent, 100000, ImGui.GetContentRegionAvail());
            ImGui.EndChild();

            ImGui.Columns(1);
            ImGui.End();
        }
    }
}
