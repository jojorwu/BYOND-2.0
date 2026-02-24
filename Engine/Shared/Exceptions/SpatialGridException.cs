using System;

namespace Shared.Exceptions;

public class SpatialGridException : EngineException
{
    public SpatialGridException(string message) : base(message) { }
    public SpatialGridException(string message, Exception innerException) : base(message, innerException) { }
}
