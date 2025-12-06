namespace Shared
{
    public interface IObjectApi
    {
        GameObject? CreateObject(string typeName, int x, int y, int z);
        GameObject? GetObject(int id);
        void DestroyObject(int id);
        void MoveObject(int id, int x, int y, int z);
    }
}
