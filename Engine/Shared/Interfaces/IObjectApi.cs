using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Interfaces
{
    public interface IObjectApi
    {
        GameObject? CreateObject(int typeId, int x, int y, int z);
        GameObject? GetObject(int id);
        void DestroyObject(int id);
        void MoveObject(int id, int x, int y, int z);
    }
}
