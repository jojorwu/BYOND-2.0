using System.Collections.Generic;
using Shared.Services;

namespace Client.Assets
{
    public class IconCache : EngineService
    {
        private readonly Dictionary<string, (string, string)> _cache = new();

        public (string, string) ParseIconString(string icon)
        {
            if (_cache.TryGetValue(icon, out var result))
            {
                return result;
            }

            var parts = icon.Split(':');
            if (parts.Length == 2)
            {
                result = (parts[0], parts[1]);
            }
            else if (parts.Length == 1)
            {
                result = (parts[0], "");
            }
            else
            {
                result = ("", "");
            }

            _cache[icon] = result;
            return result;
        }
    }
}
