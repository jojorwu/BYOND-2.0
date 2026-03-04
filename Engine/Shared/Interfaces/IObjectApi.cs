namespace Shared;
    public interface IObjectApi
    {
        GameObject? CreateObject(int typeId, long x, long y, long z);
        GameObject? GetObject(long id);
        void DestroyObject(long id);
        void MoveObject(long id, long x, long y, long z);
    }
