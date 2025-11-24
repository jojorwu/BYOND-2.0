using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        /// Gets or sets the path to the last opened project.
        /// </summary>
        public string? LastProjectPath { get; set; }

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
            var json = JsonSerializer.Serialize(this, typeof(EngineSettings), JsonContext.Default);
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
                return (EngineSettings?)JsonSerializer.Deserialize(json, typeof(EngineSettings), JsonContext.Default) ?? new EngineSettings();
            }
            return new EngineSettings();
        }
    }
}
