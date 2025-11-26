using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMCompiler;
using DMCompiler.Compiler;

namespace Core
{
    public class OpenDreamCompilerService
    {
        public (string? JsonPath, List<string> Messages) Compile(List<string> dmFiles)
        {
            if (dmFiles == null || dmFiles.Count == 0)
            {
                return (null, new List<string>()); // No files to compile, so no output path
            }

            var projectDir = Path.GetDirectoryName(dmFiles.First());
            if (projectDir == null) {
                return (null, new List<string> { "Could not determine project directory." });
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

            var messages = compiler.CompilerMessages.Select(m => m.ToString()).ToList();

            if (success)
            {
                return (jsonOutputPath, messages);
            }
            else
            {
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

                return (null, messages);
            }
        }
    }
}
