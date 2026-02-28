namespace Shared;
    public interface IObjectApi
    {
        GameObject? CreateObject(int typeId, int x, int y, int z);
        GameObject? GetObject(long id);
        void DestroyObject(long id);
        void MoveObject(long id, int x, int y, int z);
    }
