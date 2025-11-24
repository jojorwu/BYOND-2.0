namespace Core
{
    public class Project
    {
        public string RootPath { get; }

        public Project(string rootPath)
        {
            RootPath = rootPath;
        }

        public string GetFullPath(string relativePath)
        {
            return System.IO.Path.Combine(RootPath, relativePath);
        }
    }
}
