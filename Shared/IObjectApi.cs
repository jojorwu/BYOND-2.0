namespace Shared
{
    public interface IObjectApi
    {
        IGameObject? CreateObject(int typeId, int x, int y, int z);
        IGameObject? GetObject(int id);
        void DestroyObject(int id);
        void MoveObject(int id, int x, int y, int z);
    }
}
