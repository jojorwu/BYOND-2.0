
using ImGuiNET;
using Core;

namespace Editor.UI
{
    public class ScriptEditorPanel
    {
        private readonly ScriptManager _scriptManager;
        private string[] _scriptFiles;
        private string? _selectedScript;
        private string _scriptContent = string.Empty;

        public ScriptEditorPanel(ScriptManager scriptManager)
        {
            _scriptManager = scriptManager;
            _scriptFiles = _scriptManager.GetScriptFiles();
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
                    _scriptContent = _scriptManager.ReadScriptContent(scriptFile);
                }
            }
            ImGui.EndChild();

            ImGui.NextColumn();

            ImGui.BeginChild("ScriptContent");
            if (_selectedScript != null)
            {
                if (ImGui.Button("Save"))
                {
                    _scriptManager.WriteScriptContent(_selectedScript, _scriptContent);
                }
                ImGui.SameLine();
                ImGui.Text(_selectedScript);
            }
            else
            {
                ImGui.Text("No script selected");
            }

            ImGui.InputTextMultiline("##ScriptContent", ref _scriptContent, 100000, ImGui.GetContentRegionAvail());
            ImGui.EndChild();

            ImGui.Columns(1);
            ImGui.End();
        }
    }
}
