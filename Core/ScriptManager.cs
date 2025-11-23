using System;
using System.IO;
using System.Linq;

namespace Core
{
    /// <summary>
    /// Manages the listing, reading, and writing of game script files.
    /// </summary>
    public class ScriptManager
    {
        private const string ScriptDirectory = "scripts";

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptManager"/> class.
        /// </summary>
        public ScriptManager()
        {
            if (!Directory.Exists(ScriptDirectory))
            {
                Directory.CreateDirectory(ScriptDirectory);
            }
        }

        /// <summary>
        /// Gets the filenames of all Lua script files in the script directory.
        /// </summary>
        /// <returns>An array of script filenames.</returns>
        public string[] GetScriptFiles()
        {
            try
            {
                return Directory.GetFiles(ScriptDirectory, "*.lua")
                                .Select(Path.GetFileName)
                                .ToArray()!;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting script files: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Reads the content of a specified script file.
        /// </summary>
        /// <param name="fileName">The name of the file to read.</param>
        /// <returns>The content of the file, or an empty string if an error occurs.</returns>
        public string ReadScriptContent(string fileName)
        {
            try
            {
                string filePath = Path.Combine(ScriptDirectory, fileName);
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading script '{fileName}': {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Writes content to a specified script file.
        /// </summary>
        /// <param name="fileName">The name of the file to write to.</param>
        /// <param name="content">The content to write to the file.</param>
        /// <returns>True if the write operation was successful; otherwise, false.</returns>
        public bool WriteScriptContent(string fileName, string content)
        {
            try
            {
                string filePath = Path.Combine(ScriptDirectory, fileName);
                File.WriteAllText(filePath, content);
                Console.WriteLine($"Successfully saved script: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to script '{fileName}': {ex.Message}");
                return false;
            }
        }
    }
}
