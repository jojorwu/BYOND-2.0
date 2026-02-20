using System;

namespace Shared.Exceptions;

public class ComponentException : EngineException
{
    public ComponentException(string message) : base(message) { }
    public ComponentException(string message, Exception innerException) : base(message, innerException) { }
}
