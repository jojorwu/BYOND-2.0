using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Shared.Interfaces
{
    public interface IProject
    {
        string RootPath { get; }
        string GetFullPath(string relativePath);
        List<string> GetDmFiles();
    }
}
