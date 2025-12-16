using Microsoft.AspNetCore.Components;
using WebfrontCore.Components.Shared;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Layout;

public partial class NavMenu
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IZeroJsInterop JS { get; set; }
    public ActionModal ActionModal { get; set; }
    private NavigationData? NavData;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            NavData = await Api.GetNavigationDataAsync();
            if (NavData?.User != null)
            {
                AppState.SetUser(NavData.User);
            }

            if (NavData?.Localization != null)
            {
                AppState.SetLocalization(NavData.Localization);
            }
        }
        catch
        {
            // Handle error (offline/api fail)
        }

        AppState.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        AppState.OnChange -= StateHasChanged;
    }

    private async Task ToggleSidebar()
    {
        await JS.ToggleSidebar();
    }

    private string Loc(string key)
    {
        return AppState.Loc(key);
    }
}
