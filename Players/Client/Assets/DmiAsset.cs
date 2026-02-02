using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Core.Dmi;

namespace Client.Assets;

public class DmiAsset
{
    public uint TextureId { get; }
    public int Width { get; }
    public int Height { get; }
    public DmiParser.ParsedDMIDescription Description { get; }

    public DmiAsset(uint textureId, int width, int height, DmiParser.ParsedDMIDescription description)
    {
        TextureId = textureId;
        Width = width;
        Height = height;
        Description = description;
    }
}
