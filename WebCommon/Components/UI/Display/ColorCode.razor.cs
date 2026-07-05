using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace WebCommon.Components.UI.Display;

// Renders Call of Duty colour-coded strings (^1, ^2, …) to styled spans. Output uses the
// host's text-color-code-{n} CSS classes (available on every page), so it styles correctly
// for plugin pages rendered inside the host shell.
public partial class ColorCode
{
    [Parameter, EditorRequired] public string Value { get; set; } = default!;

    // We assume enabled for now, or inject config - TODO: This seems redundant at this point.
    private bool _allow = true;
    private string RenderedHtml => ProcessColorCodes();

    // Regex to match icon placeholders like :dpad_right:, :xboxx:, etc.
    private static readonly Regex IconPlaceholderRegex = new(@":[a-zA-Z0-9_]+:", RegexOptions.Compiled);

    private string ProcessColorCodes()
    {
        if (string.IsNullOrEmpty(Value)) return string.Empty;

        // First strip any icon placeholders
        var cleanValue = StripIconPlaceholders(Value);

        if (!_allow)
        {
            return WebUtility.HtmlEncode(StripColors(cleanValue));
        }

        var matches = Regex.Matches(cleanValue, @"\^([0-9]|\:)([^\^]*)");
        if (matches.Count <= 1)
        {
            return WebUtility.HtmlEncode(StripColors(cleanValue));
        }

        var sb = new System.Text.StringBuilder();
        foreach (Match match in matches)
        {
            var colorCodeChar = match.Groups[1].ToString().Last();
            var code = (colorCodeChar >= 48 && colorCodeChar <= 57) ? colorCodeChar.ToString() : ((int)colorCodeChar).ToString();
            var text = WebUtility.HtmlEncode(match.Groups[2].ToString());

            sb.Append($"<span class='text-color-code-{code}'>{text}</span>");
        }

        return sb.ToString();
    }

    private static string StripColors(string input)
    {
        // Simple strip implementation matching shared lib if possible
        return Regex.Replace(input, @"\^[0-9:]", "");
    }

    private static string StripIconPlaceholders(string input)
    {
        // Strip emoji-style icon placeholders like :dpad_right:, :xboxx:, etc.
        return IconPlaceholderRegex.Replace(input, "");
    }
}
