
using ImGuiNET;
using Core;

namespace Editor.UI
{
    public class ToolboxPanel
    {
        private readonly ToolManager _toolManager;
        private readonly Editor _editor;
        private readonly EditorContext _editorContext;

        public ToolboxPanel(ToolManager toolManager, Editor editor, EditorContext editorContext)
        {
            _toolManager = toolManager;
            _editor = editor;
            _editorContext = editorContext;
        }

        public void Draw()
        {
            ImGui.Begin("Tools");
            if (_editorContext == null) return;
            foreach (var tool in _toolManager.Tools)
            {
                if (ImGui.Button(tool.Name))
                {
                    _toolManager.SetActiveTool(tool, _editor, _editorContext);
                }
            }
            ImGui.End();
        }
    }
}
