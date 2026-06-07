using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SharedLibraryCore.Dtos;

namespace WebCommon.Components.UI.Navigation;

public partial class SideContextMenu
{
    [Parameter, EditorRequired] public SideContextMenuItems Model { get; set; } = default!;
    [Parameter] public EventCallback<SideContextMenuItem> OnActionSelect { get; set; }

    /// <summary>
    /// Optional localizer for collapse-group headers (keyed <c>GAME_{meta}</c>). The host passes
    /// <c>AppState.Loc</c>; plugins may omit it, in which case the raw key is shown.
    /// </summary>
    [Parameter] public Func<string, string>? Localizer { get; set; }

    private string Localize(string key) => Localizer?.Invoke(key) ?? key;

    private async Task OnActionClick(SideContextMenuItem item, MouseEventArgs e)
    {
        if (item.IsLink)
        {
            return;
        }

        if (OnActionSelect.HasDelegate)
        {
            await OnActionSelect.InvokeAsync(item);
        }
    }
}
