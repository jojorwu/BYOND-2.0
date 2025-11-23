using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Linq;

namespace Core
{
    public class AssetManager
    {
        private readonly Project _project;
        private string AssetsPath => _project.GetFullPath("assets");

        public AssetManager(Project project)
        {
            _project = project;
        }

        /// <summary>
        /// Imports an image from a given path, removes the background, and saves it as a new asset.
        /// </summary>
        /// <param name="sourceImagePath">The path to the source image file.</param>
        /// <returns>The relative path to the new asset file, or an empty string if import fails.</returns>
        public string ImportAsset(string sourceImagePath)
        {
            try
            {
                using (var image = Image.Load<Rgba32>(sourceImagePath))
                {
                    Rgba32 backgroundColor = image[0, 0];
                    image.Mutate(ctx =>
                    {
                        ctx.ProcessPixelRowsAsVector4(row =>
                        {
                            for (int i = 0; i < row.Length; i++)
                            {
                                if (row[i].Equals(backgroundColor.ToVector4()))
                                {
                                    row[i].W = 0;
                                }
                            }
                        });
                    });

                    string newFileName = $"{Path.GetFileNameWithoutExtension(sourceImagePath)}_{Guid.NewGuid().ToString().Substring(0, 8)}.png";
                    string newAssetPath = Path.Combine(AssetsPath, newFileName);

                    image.SaveAsPng(newAssetPath);
                    Console.WriteLine($"Successfully imported asset to {newAssetPath}");
                    return Path.GetRelativePath(_project.RootPath, newAssetPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing asset from '{sourceImagePath}': {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the relative paths of all asset files.
        /// </summary>
        /// <returns>An array of asset file paths.</returns>
        public string[] GetAssetPaths()
        {
            if (!Directory.Exists(AssetsPath))
            {
                return Array.Empty<string>();
            }
            return Directory.GetFiles(AssetsPath, "*.png");
        }
    }
}
