using System.Threading;

namespace Core
{
    public class GameObject
    {
        private static int nextId = 1;

        public int Id { get; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public GameObject(string name, int x, int y, int z)
        {
            Id = Interlocked.Increment(ref nextId);
            Name = name;
            X = x;
            Y = y;
            Z = z;
        }

        public void SetPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
