using Shared;

namespace Core.VM.Runtime
{
    public struct InlineCacheEntry
    {
        public ObjectType? Type;
        public int Index;
        public object? CachedObject;
    }
}
