
using ImGuiNET;
using Core;

namespace Editor.UI
{
    public class ToolboxPanel
    {
        private readonly ToolManager _toolManager;
        private readonly Editor _editor;

        public ToolboxPanel(ToolManager toolManager, Editor editor)
        {
            _toolManager = toolManager;
            _editor = editor;
        }

        public void Draw()
        {
            ImGui.Begin("Tools");
            foreach (var tool in _toolManager.Tools)
            {
                if (ImGui.Button(tool.Name))
                {
                    _toolManager.SetActiveTool(tool, _editor);
                }
            }
            ImGui.End();
        }
    }
}
