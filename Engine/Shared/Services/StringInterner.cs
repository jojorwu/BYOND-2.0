using System.Collections.Concurrent;

namespace Shared.Services
{
    public class StringInterner
    {
        private readonly ConcurrentDictionary<string, string> _strings = new();

        public string Intern(string value)
        {
            if (value == null) return null!;
            return _strings.GetOrAdd(value, value);
        }

        public void Clear()
        {
            _strings.Clear();
        }
    }
}
