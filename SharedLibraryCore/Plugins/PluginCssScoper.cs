using System.Text;

namespace SharedLibraryCore.Plugins;

/// <summary>
/// Rewrites a plugin's stylesheet so its rules only take effect inside that plugin's own rendered
/// DOM subtree — the element the host marks with <c>data-iw4m-plugin="{id}"</c> — using the native
/// CSS <c>@scope</c> at-rule.
///
/// <para>
/// Why this exists: the host's webfront and a plugin are two independent CSS builds sharing one
/// global class namespace. If a plugin's utilities are placed in a cascade layer <i>below</i> the
/// host's (so they can't disturb the host chrome), they also can't override the host's own base
/// utilities for the plugin's pages — e.g. a plugin's <c>sm:flex-row</c> can never beat the host's
/// <c>flex-col</c>. Placed <i>above</i>, they fix the plugin's layout but the plugin's bare
/// <c>hidden</c>/<c>flex</c> clobber the host's responsive chrome (the sidebar collapses). Cascade
/// layers alone cannot satisfy both; only scoping can. Wrapping the plugin's CSS in
/// <c>@scope ([data-iw4m-plugin="{id}"])</c> gives it full power within its own subtree and zero
/// reach outside it, so a plugin may freely use any class — even ones the host uses — regardless of
/// CSS provider.
/// </para>
///
/// <para>
/// At-rules that are illegal or pointless nested inside <c>@scope</c> are hoisted back to the top
/// level: <c>@import</c>/<c>@charset</c>/<c>@namespace</c> (must lead the sheet), <c>@property</c>
/// (custom-property registrations are document-global), <c>@keyframes</c> and <c>@font-face</c>
/// (global by name). <c>:root</c>/<c>:host</c> token definitions are also kept global so a plugin's
/// design tokens resolve even if the host doesn't define them. Everything else is scoped.
/// </para>
/// </summary>
public static class PluginCssScoper
{
    /// <summary>UTF-8 convenience overload for the in-memory asset pipeline (bytes in, bytes out).</summary>
    public static byte[] ScopeCss(byte[] css, string pluginId) =>
        Encoding.UTF8.GetBytes(ScopeCss(Encoding.UTF8.GetString(css), pluginId));

    /// <summary>The attribute selector the host stamps on a plugin's content wrapper.</summary>
    public static string MarkerSelectorFor(string pluginId) => $"[data-iw4m-plugin=\"{pluginId}\"]";

    public static string ScopeCss(string css, string pluginId)
    {
        if (string.IsNullOrWhiteSpace(css) || string.IsNullOrWhiteSpace(pluginId))
        {
            return css;
        }

        var globals = new StringBuilder();
        var scoped = new StringBuilder();
        foreach (var segment in TopLevelSegments(css))
        {
            (IsHoistedGlobal(segment) ? globals : scoped).Append(segment);
        }

        if (scoped.Length == 0)
        {
            return css; // nothing scope-able (e.g. a sheet of only @keyframes); leave untouched
        }

        var result = new StringBuilder(css.Length + 64);
        if (globals.Length > 0)
        {
            result.Append(globals).Append('\n');
        }

        result.Append("@scope (").Append(MarkerSelectorFor(pluginId)).Append(") {\n")
              .Append(scoped).Append("\n}\n");
        return result.ToString();
    }

    // A top-level segment that must stay outside @scope (see class summary).
    private static bool IsHoistedGlobal(string segment)
    {
        var s = SkipLeadingTrivia(segment);
        if (s.Length == 0)
        {
            return false;
        }

        if (s[0] == '@')
        {
            return StartsWith(s, "@import") || StartsWith(s, "@charset") || StartsWith(s, "@namespace")
                || StartsWith(s, "@property") || StartsWith(s, "@font-face")
                || StartsWith(s, "@keyframes") || StartsWith(s, "@-webkit-keyframes")
                || StartsWith(s, "@-moz-keyframes") || StartsWith(s, "@-o-keyframes");
        }

        // keep :root / :host custom-property (design token) definitions global
        return StartsWith(s, ":root") || StartsWith(s, ":host");
    }

    private static bool StartsWith(string s, string prefix) =>
        s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static string SkipLeadingTrivia(string segment)
    {
        var s = segment.TrimStart();
        while (s.StartsWith("/*", StringComparison.Ordinal))
        {
            var end = s.IndexOf("*/", 2, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            s = s[(end + 2)..].TrimStart();
        }

        return s;
    }

    // Splits a stylesheet into top-level segments — each either an at-statement ending in ';' or a
    // rule/block ending at its matching '}'. String-, comment- and escape-aware so that braces or
    // semicolons inside strings, comments or escaped selectors (e.g. .sm\:flex-row) don't mis-split.
    // Trivia between segments rides along with the segment it precedes.
    private static IEnumerable<string> TopLevelSegments(string css)
    {
        int n = css.Length, start = 0, i = 0, depth = 0;
        var inBlock = false;

        while (i < n)
        {
            var c = css[i];

            if (c == '/' && i + 1 < n && css[i + 1] == '*')
            {
                var end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? n : end + 2;
                continue;
            }

            if (c is '"' or '\'')
            {
                var quote = c;
                i++;
                while (i < n)
                {
                    if (css[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (css[i] == quote)
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (c == '\\')
            {
                i += 2; // escaped char (selectors like .sm\:flex-row)
                continue;
            }

            if (c == '{')
            {
                depth++;
                inBlock = true;
                i++;
                continue;
            }

            if (c == '}')
            {
                depth--;
                i++;
                if (depth == 0 && inBlock)
                {
                    yield return css[start..i];
                    start = i;
                    inBlock = false;
                }

                continue;
            }

            if (c == ';' && depth == 0 && !inBlock)
            {
                i++;
                yield return css[start..i];
                start = i;
                continue;
            }

            i++;
        }

        if (start < n)
        {
            yield return css[start..];
        }
    }
}
