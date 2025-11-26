using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMCompiler;

namespace Core
{
    public class OpenDreamCompilerService
    {
        public string? Compile(List<string> dmFiles)
        {
            if (dmFiles == null || dmFiles.Count == 0)
            {
                return null; // No files to compile, so no output path
            }

            var projectDir = Path.GetDirectoryName(dmFiles.First());
            if (projectDir == null) {
                Console.WriteLine("Could not determine project directory.");
                return null;
            }
            var dmePath = Path.Combine(projectDir, "project.dme");
            var jsonOutputPath = Path.ChangeExtension(dmePath, "json");

            // Create a temporary .dme file
            File.WriteAllLines(dmePath, dmFiles.Select(file => $"#include \"{Path.GetRelativePath(projectDir, file)}\""));

            var settings = new DMCompilerSettings
            {
                Files = new List<string> { dmePath },
                StoreMessages = true // Enable message storing
            };

            var compiler = new DMCompiler.DMCompiler();
            bool success = compiler.Compile(settings);

            // Clean up the temporary .dme file
            try
            {
                File.Delete(dmePath);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Warning: Could not delete temporary file {dmePath}: {ex.Message}");
            }

            if (success)
            {
                return jsonOutputPath;
            }
            else
            {
                Console.WriteLine("OpenDream compilation failed. See errors below:");
                foreach (var message in compiler.CompilerMessages)
                {
                    Console.WriteLine(message);
                }

                // Clean up the failed artifact
                try
                {
                    if (File.Exists(jsonOutputPath))
                    {
                        File.Delete(jsonOutputPath);
                    }
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Warning: Could not delete failed artifact {jsonOutputPath}: {ex.Message}");
                }

                return null;
            }
        }
    }
}
