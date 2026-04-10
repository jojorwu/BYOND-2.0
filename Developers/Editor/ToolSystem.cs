using System;

namespace Editor
{
    public interface ITool
    {
        string Name { get; }
        void OnSelected();
        void OnMouseDown(float x, float y);
        void OnMouseMove(float x, float y);
        void OnMouseUp(float x, float y);
    }

    public interface IToolManager
    {
        ITool? ActiveTool { get; set; }
        void RegisterTool(ITool tool);
    }

    public class ToolManager : IToolManager
    {
        private readonly List<ITool> _tools = new();
        public ITool? ActiveTool { get; set; }

        public void RegisterTool(ITool tool) => _tools.Add(tool);
    }
}
