using Robust.Shared.Maths;

namespace Shared.Interfaces;

public interface ITransform
{
    Vector3l Position { get; set; }
    long X { get; set; }
    long Y { get; set; }
    long Z { get; set; }
    int Dir { get; set; }
    IGameObject? Loc { get; set; }

    long CommittedX { get; }
    long CommittedY { get; }
    long CommittedZ { get; }
    int CommittedDir { get; }

    void SetPosition(long x, long y, long z);
}
