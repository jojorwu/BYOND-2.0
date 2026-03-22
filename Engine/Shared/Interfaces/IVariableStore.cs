using Shared;
using System;

namespace Shared.Interfaces;

/// <summary>
/// Abstraction for storing and managing object variables.
/// Supports high-performance access patterns and data-oriented layouts.
/// </summary>
public interface IVariableStore : IDisposable
{
    void Initialize(int capacity);
    DreamValue Get(int index);
    ref DreamValue GetRef(int index);
    void Set(int index, DreamValue value);
    void CopyFrom(DreamValue[] source);
    int Length { get; }
}
