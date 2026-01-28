namespace Editor
{
    public interface IProjectService
    {
        bool LoadProject(string projectPath);
        void SaveProject();
    }
}
