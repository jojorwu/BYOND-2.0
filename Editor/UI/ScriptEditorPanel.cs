
using ImGuiNET;
using Core;
using ImGuiColorTextEditNet;
using ImGuiColorTextEditNet.Syntax;

namespace Editor.UI
{
    public class ScriptEditorPanel
    {
        private readonly ScriptManager _scriptManager;
        private string[] _scriptFiles;
        private string? _selectedScript;
        private TextEditor _textEditor = new TextEditor();

        public ScriptEditorPanel(ScriptManager scriptManager)
        {
            _scriptManager = scriptManager;
            _scriptFiles = _scriptManager.GetScriptFiles();
            _textEditor.SyntaxHighlighter = new CStyleHighlighter(false);
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
                    _textEditor.AllText = _scriptManager.ReadScriptContent(scriptFile);
                }
            }
            ImGui.EndChild();

            ImGui.NextColumn();

            ImGui.BeginChild("ScriptContent");
            if (_selectedScript != null)
            {
                if (ImGui.Button("Save"))
                {
                    _scriptManager.WriteScriptContent(_selectedScript, _textEditor.AllText);
                }
                ImGui.SameLine();
                ImGui.Text(_selectedScript);
            }
            else
            {
                ImGui.Text("No script selected");
            }

            _textEditor.Render("ScriptContent");
            ImGui.EndChild();

            ImGui.Columns(1);
            ImGui.End();
        }
    }
}
