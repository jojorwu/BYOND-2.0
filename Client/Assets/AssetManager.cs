using System;
using System.Collections.Generic;
using Core.Dmi;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Client.Assets;

public class AssetManager : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, DmiAsset> _dmiCache = new();

    public AssetManager(GL gl)
    {
        _gl = gl;
    }

    public DmiAsset LoadDmi(string path)
    {
        if (_dmiCache.TryGetValue(path, out var asset))
        {
            return asset;
        }

        var dmiData = DmiLoader.Load(path);
        var textureId = CreateTextureFromImage(dmiData.Image);

        asset = new DmiAsset(textureId, dmiData.Image.Width, dmiData.Image.Height, dmiData.Description);
        _dmiCache[path] = asset;

        return asset;
    }

    private unsafe uint CreateTextureFromImage(Image<Rgba32> image)
    {
        uint textureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, textureId);

        // The GL texture coordinate system has (0, 0) at the bottom-left,
        // but ImageSharp loads with (0, 0) at the top-left. We need to flip it.
        image.Mutate(x => x.Flip(FlipMode.Vertical));

        fixed (void* data = image.GetPixelRowSpan(0))
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);

        return textureId;
    }

    public void Dispose()
    {
        foreach (var asset in _dmiCache.Values)
        {
            _gl.DeleteTexture(asset.TextureId);
        }
    }
}
