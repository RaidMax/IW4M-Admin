using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using WebfrontCore.Services;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Components.Shared;

public partial class SideContextMenu
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IZeroJsInterop JS { get; set; }

    [Parameter] public SideContextMenuItems Model { get; set; }
    [Parameter] public EventCallback<SideContextMenuItem> OnActionSelect { get; set; }

    private async Task OnActionClick(SideContextMenuItem item, MouseEventArgs e)
    {
        if (item.IsLink) return;
        if (OnActionSelect.HasDelegate)
        {
            await OnActionSelect.InvokeAsync(item);
        }
    }
}
