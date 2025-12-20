namespace WebfrontCore.Components.UI.Layout;

public partial class SidebarContainer
{
    protected override void OnInitialized()
    {
        AppState.OnChange += OnAppStateChange;
    }

    private void OnAppStateChange()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        AppState.OnChange -= OnAppStateChange;
    }

    private void ToggleMobile()
    {
        AppState.IsMobileNavOpen = false;
    }
}