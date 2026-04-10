using System.Collections.Generic;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Editor
{
    public interface IEditorUIService
    {
        void Initialize(GL gl, IWindow window);
        void Render(float dt);
        void RegisterPanel(IEditorPanel panel);
    }

    public interface IEditorPanel
    {
        string Title { get; }
        void Render();
    }

    public class EditorUIService : IEditorUIService
    {
        private readonly List<IEditorPanel> _panels = new();

        public void Initialize(GL gl, IWindow window)
        {
            // Initialize ImGui here in a real implementation
        }

        public void RegisterPanel(IEditorPanel panel) => _panels.Add(panel);

        public void Render(float dt)
        {
            foreach (var panel in _panels)
            {
                panel.Render();
            }
        }
    }
}
