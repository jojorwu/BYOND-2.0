using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Editor
{
    public class LocalizationManager
    {
        private Dictionary<string, string> _translations = new Dictionary<string, string>();

        public void LoadLanguage(string languageCode)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"Editor.assets.lang.{languageCode}.json";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var json = reader.ReadToEnd();
                        _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                    }
                }
            }
        }

        public string GetString(string key)
        {
            return _translations.TryGetValue(key, out var value) ? value : key;
        }
    }
}
