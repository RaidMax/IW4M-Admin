using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.UI.Controls;

public partial class GenericTable
{
    [Inject] public required AppState AppState { get; set; }
    [Parameter] public TableInfo Model { get; set; }
    private bool IsExpanded = false;

    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
        // In true Blazor, we would toggle a class on the rows. 
        // Implementation: Add !IsExpanded check to the hidden-row classes above.
        // Current CSS uses d-none hidden-row-lg. 
        // We should bind the class logic to IsExpanded.
    }

    private MarkupString RenderHtmlContent(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return new MarkupString(writer.ToString());
    }

    private string Loc(string key)
    {
        if (AppState.User?.ToString() != null)
        {
            // return from dictionary TODO
        }

        return key; // Placeholder
    }
}
