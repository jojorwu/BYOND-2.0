namespace Shared
{
    public interface IObjectApi
    {
        GameObject? CreateObject(int typeId, int x, int y, int z);
        GameObject? GetObject(int id);
        void DestroyObject(int id);
        void MoveObject(int id, int x, int y, int z);
    }
}
