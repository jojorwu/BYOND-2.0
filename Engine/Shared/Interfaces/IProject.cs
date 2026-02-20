using System.Collections.Generic;

namespace Shared;
    public interface IProject
    {
        string RootPath { get; }
        string GetFullPath(string relativePath);
        List<string> GetDmFiles();
    }
