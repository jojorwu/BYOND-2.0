using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Core.Dmi;

public record DmiData(Image<Rgba32> Image, DmiParser.ParsedDMIDescription Description);
