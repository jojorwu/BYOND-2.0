using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Interfaces
{
    public interface IUiPanel
    {
        string Name { get; }
        bool IsOpen { get; set; }
        void Draw();
    }
}
