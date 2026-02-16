using System.Collections.Concurrent;
using Shared.Interfaces;

namespace Shared.Services
{
    public class StringInterner : IShrinkable
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

        public void Shrink()
        {
            if (_strings.Count > MaxStrings / 2)
            {
                // Only clear if it's getting somewhat full, but be less aggressive than the hard cap
                _strings.Clear();
            }
        }
    }
}
