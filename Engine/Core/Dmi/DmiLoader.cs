using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Core.Dmi;

public static class DmiLoader
{
    public static DmiData Load(string path)
    {
        using var stream = File.OpenRead(path);

        var description = DmiParser.ParseDMI(stream);

        stream.Seek(0, SeekOrigin.Begin);

        var image = Image.Load<Rgba32>(stream);

        return new DmiData(image, description);
    }
}
