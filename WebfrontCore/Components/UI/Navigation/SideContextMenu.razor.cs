using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.UI.Navigation;

public partial class SideContextMenu
{
    [Inject] public required AppState AppState { get; set; }

    [Parameter] public SideContextMenuItems Model { get; set; }
    [Parameter] public EventCallback<SideContextMenuItem> OnActionSelect { get; set; }

    private bool _isOpen;

    private void ToggleMobileMenu()
    {
        _isOpen = !_isOpen;
        StateHasChanged();
    }

    private async Task OnActionClick(SideContextMenuItem item, MouseEventArgs e)
    {
        // Close mobile drawer when an item is clicked
        _isOpen = false;
        
        if (item.IsLink) return;
        if (OnActionSelect.HasDelegate)
        {
            await OnActionSelect.InvokeAsync(item);
        }
    }
}
