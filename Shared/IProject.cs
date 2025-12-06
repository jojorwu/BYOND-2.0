using System.Collections.Generic;

namespace Core
{
    public interface IProject
    {
        string RootPath { get; }
        string GetFullPath(string relativePath);
        List<string> GetDmFiles();
    }
}
