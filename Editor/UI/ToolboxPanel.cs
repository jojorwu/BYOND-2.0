using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class ToolboxPanel
    {
        private readonly ToolManager _toolManager;
        private readonly EditorContext _editorContext;

        public ToolboxPanel(ToolManager toolManager, EditorContext editorContext)
        {
            _toolManager = toolManager;
            _editorContext = editorContext;
        }

        public void Draw()
        {
            ImGui.Begin("Tools");
            foreach (var tool in _toolManager.Tools)
            {
                if (ImGui.Button(tool.Name))
                {
                    _toolManager.SetActiveTool(tool, _editorContext);
                }
            }
            ImGui.End();
        }
    }
}
