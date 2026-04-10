using Silk.NET.OpenGL;
using System.Numerics;
using Shared.Attributes;
using Shared.Services;

namespace Editor
{
    [EngineService]
    public class EditorRenderer : EngineService
    {
        private GL? _gl;

        public void Initialize(GL gl)
        {
            _gl = gl;
        }

        public void RenderGrid(int size, float opacity)
        {
            // Grid rendering logic
        }

        public void RenderGizmos()
        {
            // Gizmo rendering logic
        }

        public void HighlightSelection(long entityId)
        {
            // Selection highlighting logic
        }
    }
}
