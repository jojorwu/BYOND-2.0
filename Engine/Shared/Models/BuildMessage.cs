namespace Shared;
    public record BuildMessage(string File, int Line, string Text, BuildMessageLevel Level);

    public enum BuildMessageLevel
    {
        Info,
        Warning,
        Error
    }
