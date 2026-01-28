namespace Shared;

/// <summary>
/// Handles how we write format data into our strings.
/// </summary>
public static class StringFormatEncoder {
    /// <summary>
    /// This is the upper byte of the 2-byte markers we use for storing formatting data within our UTF16 strings.
    /// </summary>
    public const ushort FormatPrefix = 0xFF00;

    public enum FormatSuffix : ushort {
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

        ColorRed = 0x17,
        ColorBlue = 0x18,
        ColorGreen = 0x19,
        ColorBlack = 0x1A,
        ColorYellow = 0x1B,
        ColorNavy = 0x1C,
        ColorTeal = 0x1D,
        ColorCyan = 0x1E,
        Bold = 0x1F,
        Italic = 0x20,
    }

    public const FormatSuffix InterpolationDefault = FormatSuffix.StringifyWithArticle;

    public static char Encode(FormatSuffix suffix) {
        return (char)(FormatPrefix | (ushort)suffix);
    }

    public static bool Decode(char c, out FormatSuffix suffix) {
        ushort val = c;
        if ((val & 0xFF00) == FormatPrefix) {
            suffix = (FormatSuffix)(val & 0xFF);
            return true;
        }

        suffix = default;
        return false;
    }

    public static bool IsInterpolation(FormatSuffix suffix) {
        return suffix is FormatSuffix.StringifyWithArticle or FormatSuffix.StringifyNoArticle or FormatSuffix.ReferenceOfValue or FormatSuffix.NoStringify;
    }
}
