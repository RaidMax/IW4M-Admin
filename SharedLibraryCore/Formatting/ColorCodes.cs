using System.Collections.Generic;

namespace SharedLibraryCore.Formatting
{
    public enum ColorCodes
    {
        Black,
        Red,
        Green,
        Yellow,
        Blue,
        Cyan,
        Purple,
        Pink,
        White,
        Map,
        Grey,
        Wildcard,
        Accent,
        Orange
    }

    public class ColorCodeMapping : Dictionary<string, string>
    {
    }
}