namespace PunchPoint.Core.Jet;

/// <summary>Thrown when a file cannot be read as a Jet 4 database.</summary>
public sealed class Jet4FormatException : Exception
{
    public Jet4FormatException(string message) : base(message) { }
    public Jet4FormatException(string message, Exception inner) : base(message, inner) { }
}
