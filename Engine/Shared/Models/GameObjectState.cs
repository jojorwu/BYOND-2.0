namespace Shared.Models;

public struct GameObjectTransformState
{
    public Robust.Shared.Maths.Vector3l Position;
    public float Rotation;
}

public struct GameObjectVisualState
{
    public string Icon;
    public string IconState;
    public int Dir;
    public double Alpha;
    public string Color;
    public double Layer;
    public double PixelX;
    public double PixelY;
    public double Opacity;
}

public struct CommittedGameObjectState
{
    public GameObjectTransformState Transform;
    public GameObjectVisualState Visuals;
}
