using Core;

namespace Editor
{
    public class EditorContext
    {
        public int CurrentZLevel { get; set; } = 0;
        public ObjectType? SelectedObjectType { get; set; }
    }
}
