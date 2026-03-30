using System.Numerics;
using Shared.Models;

namespace Shared.Components;

public class TransformComponent : BaseComponent
{
    private Vector3 _position;
    private Vector3 _nextPosition;
    private Quaternion _rotation;
    private Quaternion _nextRotation;

    public Vector3 Position
    {
        get => _position;
        set => _nextPosition = value;
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set => _nextRotation = value;
    }

    // Direct access to the current state for interpolation or rendering
    public Vector3 CurrentPosition => _position;
    public Quaternion CurrentRotation => _rotation;

    public override void BeginUpdate()
    {
        _nextPosition = _position;
        _nextRotation = _rotation;
    }

    public override void CommitUpdate()
    {
        _position = _nextPosition;
        _rotation = _nextRotation;
    }

    public override void Reset()
    {
        base.Reset();
        _position = Vector3.Zero;
        _nextPosition = Vector3.Zero;
        _rotation = Quaternion.Identity;
        _nextRotation = Quaternion.Identity;
    }
}
