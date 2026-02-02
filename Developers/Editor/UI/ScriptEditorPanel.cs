using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using ImGuiNET;
using System.IO;

namespace Editor.UI
{
    public class ScriptEditorPanel
    {
        private string _text = "";
        private string _currentFile = "";

        public void Draw(string filePath)
        {
            if (_currentFile != filePath)
            {
                _text = File.ReadAllText(filePath);
                _currentFile = filePath;
            }

            if (ImGui.Button("Save"))
            {
                File.WriteAllText(_currentFile, _text);
            }

            ImGui.InputTextMultiline("##ScriptEditor", ref _text, 100000, new System.Numerics.Vector2(-1, -1));
        }
    }
}
