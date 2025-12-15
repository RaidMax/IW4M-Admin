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
    [CascadingParameter] public ActionModal ActionModal { get; set; }

    private async Task OnActionClick(SideContextMenuItem item, MouseEventArgs e)
    {
        if (item.IsLink) return;

        if (ActionModal != null && !string.IsNullOrEmpty(item.Reference))
        {
            // item.Reference e.g. "BanForm" or "edit".
            // We pass it to Open, which handles fetching /Action/{Name}Form
            await ActionModal.Open(item.Reference, item.EntityId, item.Meta);
        }
    }
}
