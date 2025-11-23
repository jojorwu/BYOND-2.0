using System;
using System.IO;
using Newtonsoft.Json;

namespace Core
{
    /// <summary>
    /// Holds engine-wide configuration settings.
    /// </summary>
    public class EngineSettings
    {
        private const string SettingsFilePath = "engine_settings.json";

        /// <summary>
        /// Gets or sets a value indicating whether multi-threading is enabled for performance-critical tasks.
        /// </summary>
        public bool EnableMultiThreading { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of threads to use for parallel tasks.
        /// If set to 0, the engine will automatically determine the number of threads based on the processor count.
        /// </summary>
        public int NumberOfThreads { get; set; } = 0;

        /// <summary>
        /// Gets the effective number of threads to use, considering the user's settings and the system's capabilities.
        /// </summary>
        [JsonIgnore]
        public int EffectiveNumberOfThreads => NumberOfThreads > 0 ? NumberOfThreads : Environment.ProcessorCount;

        /// <summary>
        /// Saves the current settings to a file.
        /// </summary>
        public void Save()
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
        }

        /// <summary>
        /// Loads the settings from a file, or returns a new instance with default values if the file does not exist.
        /// </summary>
        /// <returns>The loaded or new engine settings.</returns>
        public static EngineSettings Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonConvert.DeserializeObject<EngineSettings>(json) ?? new EngineSettings();
            }
            return new EngineSettings();
        }
    }
}
