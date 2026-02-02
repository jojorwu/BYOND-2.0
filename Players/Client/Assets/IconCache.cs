using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Client.Assets
{
    public static class IconCache
    {
        private static readonly Dictionary<string, (string, string)> _cache = new();

        public static (string, string) ParseIconString(string icon)
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
