using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Utils;

public readonly struct VfsPath : IEquatable<VfsPath>
{
    private readonly string _normalized;

    public VfsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _normalized = "";
            return;
        }

        // Normalize to use forward slashes and remove redundant segments
        _normalized = path.Replace('\\', '/').Trim('/');
        _normalized = string.Join("/", _normalized.Split('/', StringSplitOptions.RemoveEmptyEntries));
    }

    public string Value => _normalized;
    public bool IsEmpty => string.IsNullOrEmpty(_normalized);
    public string Name => Path.GetFileName(_normalized);
    public string Extension => Path.GetExtension(_normalized);
    public VfsPath Parent => new(Path.GetDirectoryName(_normalized) ?? "");

    public VfsPath Join(string other) => new(Path.Combine(_normalized, other.Replace('\\', '/').Trim('/')));

    public bool Equals(VfsPath other) => _normalized.Equals(other._normalized, StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => obj is VfsPath other && Equals(other);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_normalized);
    public override string ToString() => _normalized;

    public static implicit operator string(VfsPath path) => path._normalized;
    public static implicit operator VfsPath(string path) => new(path);
    public static bool operator ==(VfsPath left, VfsPath right) => left.Equals(right);
    public static bool operator !=(VfsPath left, VfsPath right) => !left.Equals(right);
}
