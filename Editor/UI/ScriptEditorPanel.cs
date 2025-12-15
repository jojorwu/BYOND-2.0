using ImGuiNET;
using System.IO;

namespace Editor.UI
{
    public class ScriptEditorPanel : IUiPanel
    {
        private readonly EditorContext _editorContext;
        private string _text = "";
        private string _currentFile = "";

        public ScriptEditorPanel(EditorContext editorContext)
        {
            _editorContext = editorContext;
        }

        public void Draw()
        {
            if (string.IsNullOrEmpty(_editorContext.SelectedFile))
            {
                ImGui.Text("No file selected.");
                return;
            }

            if (_currentFile != _editorContext.SelectedFile)
            {
                _text = File.ReadAllText(_editorContext.SelectedFile);
                _currentFile = _editorContext.SelectedFile;
            }

            if (ImGui.Button("Save"))
            {
                File.WriteAllText(_currentFile, _text);
            }

            ImGui.InputTextMultiline("##ScriptEditor", ref _text, 100000, new System.Numerics.Vector2(-1, -1));
        }
    }
}
