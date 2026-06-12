using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;

namespace WebCommon.Components.UI.Controls;

public partial class GenericTable
{
    [Parameter, EditorRequired] public TableInfo Model { get; set; } = default!;

    /// <summary>
    /// Optional translator for the component's own static strings (e.g. the "no data" placeholder).
    /// The host passes its localizer; out-of-tree callers can leave it null and get the raw key back.
    /// </summary>
    [Parameter] public Func<string, string>? Localizer { get; set; }

    private bool IsExpanded;

    // The actual show/hide of the rows past InitialRowCount is done by the host's legacy
    // `.table-slide` click handler (jQuery in global.min.js, loaded on every page) — it toggles
    // the d-none / hidden-row(-lg) classes, so the behaviour also works for plugin pages rendered
    // inside the host shell. This flag only tracks the caret direction across re-renders.
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    private MarkupString RenderHtmlContent(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return new MarkupString(writer.ToString());
    }

    private string Loc(string key) => Localizer?.Invoke(key) ?? key;
}
