using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Editor
{
    public interface IProjectService
    {
        bool LoadProject(string projectPath);
        void SaveProject();
    }
}
