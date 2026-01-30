using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Core.Dmi;

public record DmiData(Image<Rgba32> Image, DmiParser.ParsedDMIDescription Description);
