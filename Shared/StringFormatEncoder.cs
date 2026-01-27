namespace Shared;

/// <summary>
/// This is a simple class to help with the encoding of string formatting macros.
/// It works by having a special start character (0xFF) followed by a byte representing the macro.
/// </summary>
public static class StringFormatEncoder {
    public const char StartChar = (char)0xFF;

    public enum FormatSuffix : byte {
        StringifyWithArticle = 0x0, // []
        StringifyNoArticle = 0x1,   // \roman []
        ReferenceOfValue = 0x2,     // \ref []
        NoStringify = 0x3,          // \icon []

        UpperDefiniteArticle = 0x4,     // \The
        LowerDefiniteArticle = 0x5,     // \the
        UpperIndefiniteArticle = 0x6,   // \A, \An
        LowerIndefiniteArticle = 0x7,   // \a, \an

        UpperSubjectPronoun = 0x8,  // \He, \She
        LowerSubjectPronoun = 0x9,  // \he, \she
        UpperPossessiveAdjective = 0xA, // \His
        LowerPossessiveAdjective = 0xB, // \his
        ObjectPronoun = 0xC,            // \him
        ReflexivePronoun = 0xD,         // \himself, \herself
        UpperPossessivePronoun = 0xE,   // \Hers
        LowerPossessivePronoun = 0xF,   // \hers

        PluralSuffix = 0x10,        // \s
        OrdinalIndicator = 0x11,    // \th

        Proper = 0x12,   // \proper
        Improper = 0x13, // \improper

        LowerRoman = 0x14, // \roman
        UpperRoman = 0x15, // \Roman

        Icon = 0x16, // \icon
    }

    public const FormatSuffix InterpolationDefault = FormatSuffix.StringifyWithArticle;

    public static string Encode(FormatSuffix suffix) {
        return $"{StartChar}{(char)suffix}";
    }

    public static bool Decode(char c, out FormatSuffix suffix) {
        suffix = (FormatSuffix)c;
        return Enum.IsDefined(typeof(FormatSuffix), suffix);
    }

    public static bool IsInterpolation(FormatSuffix suffix) {
        return suffix is FormatSuffix.StringifyWithArticle or FormatSuffix.StringifyNoArticle or FormatSuffix.ReferenceOfValue or FormatSuffix.NoStringify;
    }
}
