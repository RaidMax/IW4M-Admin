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
            switch (Classify(segment))
            {
                case SegmentKind.GlobalAtRule:
                    globals.Append(segment);
                    break;
                case SegmentKind.RootTokens:
                    // Hoist :root/:host token definitions global so a plugin's design tokens resolve,
                    // but strip self-references (--x: var(--x)) — those come from the host-token theme
                    // and, left in a global :root loaded after the host's, would override the host's
                    // real token values with a circular reference and break them everywhere.
                    globals.Append(StripSelfReferences(segment));
                    break;
                default:
                    scoped.Append(segment);
                    break;
            }
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

    private enum SegmentKind
    {
        Scoped,
        GlobalAtRule,
        RootTokens
    }

    // Classify a top-level segment for placement (see class summary).
    private static SegmentKind Classify(string segment)
    {
        var s = SkipLeadingTrivia(segment);
        if (s.Length == 0)
        {
            return SegmentKind.Scoped;
        }

        if (s[0] == '@')
        {
            var global = StartsWith(s, "@import") || StartsWith(s, "@charset") || StartsWith(s, "@namespace")
                || StartsWith(s, "@property") || StartsWith(s, "@font-face")
                || StartsWith(s, "@keyframes") || StartsWith(s, "@-webkit-keyframes")
                || StartsWith(s, "@-moz-keyframes") || StartsWith(s, "@-o-keyframes");
            return global ? SegmentKind.GlobalAtRule : SegmentKind.Scoped;
        }

        return StartsWith(s, ":root") || StartsWith(s, ":host")
            ? SegmentKind.RootTokens
            : SegmentKind.Scoped;
    }

    // Drop `--x: var(--x)` self-referential declarations from a :root/:host rule, keeping the rest
    // (real default-token values). The block has no nested braces, so first '{' / last '}' bound it.
    private static string StripSelfReferences(string segment)
    {
        var open = segment.IndexOf('{');
        var close = segment.LastIndexOf('}');
        if (open < 0 || close <= open)
        {
            return segment;
        }

        var kept = new StringBuilder();
        foreach (var decl in SplitTopLevel(segment[(open + 1)..close], ';'))
        {
            if (decl.Trim().Length != 0 && !IsSelfReference(decl))
            {
                kept.Append(decl).Append(';');
            }
        }

        return segment[..(open + 1)] + kept + segment[close..];
    }

    private static bool IsSelfReference(string decl)
    {
        var colon = decl.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        var prop = decl[..colon].Trim();
        if (!prop.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        // whitespace-insensitive compare: `--x : var( --x )` is still a self-reference
        var value = new string(decl[(colon + 1)..].Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        return value == $"var({prop})";
    }

    // Split on a separator at paren-depth 0 (so ';' inside var()/color-mix() values doesn't split).
    private static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (c == separator && depth == 0)
            {
                yield return text[start..i];
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            yield return text[start..];
        }
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
