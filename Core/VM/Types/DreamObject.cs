namespace Core.VM.Types
{
    public class DreamObject
    {
        public GameObject GameObject { get; }

        public DreamObject(GameObject gameObject)
        {
            GameObject = gameObject;
        }
    }
}
