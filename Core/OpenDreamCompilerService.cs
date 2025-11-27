using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMCompiler;

namespace Core
{
    public class OpenDreamCompilerService
    {
        public (string? JsonPath, List<DMCompiler.Compiler.CompilerEmission> Messages) Compile(List<string> dmFiles)
        {
            if (dmFiles == null || dmFiles.Count == 0)
            {
                return (null, new List<DMCompiler.Compiler.CompilerEmission>());
            }

            var settings = new DMCompilerSettings
            {
                Files = dmFiles,
                StoreMessages = true
            };

            var compiler = new DMCompiler.DMCompiler();
            var (success, outputPath) = compiler.Compile(settings);

            if (success && outputPath != null && File.Exists(outputPath))
            {
                return (outputPath, compiler.CompilerMessages);
            }
            else
            {
                if (outputPath != null && File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Warning: Could not delete failed artifact {outputPath}: {ex.Message}");
                    }
                }
                return (null, compiler.CompilerMessages);
            }
        }
    }
}
