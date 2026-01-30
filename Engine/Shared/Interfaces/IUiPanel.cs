namespace Shared
{
    public interface IUiPanel
    {
        string Name { get; }
        bool IsOpen { get; set; }
        void Draw();
    }
}
