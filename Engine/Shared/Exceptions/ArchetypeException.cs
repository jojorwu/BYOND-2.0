using System;

namespace Shared.Exceptions;

public class ArchetypeException : EngineException
{
    public ArchetypeException(string message) : base(message) { }
    public ArchetypeException(string message, Exception innerException) : base(message, innerException) { }
}
