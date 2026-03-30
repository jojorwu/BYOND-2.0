using System;

namespace Shared.Enums;

[Flags]
public enum GameObjectFields : uint
{
    None = 0,
    PositionX = 1 << 0,
    PositionY = 1 << 1,
    PositionZ = 1 << 2,
    Dir = 1 << 3,
    Alpha = 1 << 4,
    Color = 1 << 5,
    Layer = 1 << 6,
    Icon = 1 << 7,
    IconState = 1 << 8,
    PixelX = 1 << 9,
    PixelY = 1 << 10,
    Rotation = 1 << 11,
    Variables = 1 << 12,
    Type = 1 << 13,
    NewObject = 1 << 14,

    Position = PositionX | PositionY | PositionZ,
    Visuals = Dir | Alpha | Color | Layer | Icon | IconState | PixelX | PixelY
}
