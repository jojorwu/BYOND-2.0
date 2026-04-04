namespace Shared.Interfaces;

public interface IVisuals
{
    string Icon { get; set; }
    string IconState { get; set; }
    double Alpha { get; set; }
    string Color { get; set; }
    double PixelX { get; set; }
    double PixelY { get; set; }
    double Layer { get; set; }
    double Opacity { get; set; }
    float Rotation { get; set; }

    string CommittedIcon { get; }
    string CommittedIconState { get; }
    double CommittedAlpha { get; }
    string CommittedColor { get; }
    double CommittedLayer { get; }
    double CommittedPixelX { get; }
    double CommittedPixelY { get; }

    double RenderX { get; set; }
    double RenderY { get; set; }
    double RenderZ { get; set; }
}
