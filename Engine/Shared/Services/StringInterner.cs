using System.Collections.Concurrent;

namespace Shared.Services
{
    public class StringInterner
    {
        private const int MaxStrings = 10000;
        private readonly ConcurrentDictionary<string, string> _strings = new();

        public string Intern(string value)
        {
            if (value == null) return null!;

            if (_strings.Count > MaxStrings)
            {
                _strings.Clear(); // Flush to prevent leak
            }

            return _strings.GetOrAdd(value, value);
        }

        public void Clear()
        {
            _strings.Clear();
        }
    }
}
