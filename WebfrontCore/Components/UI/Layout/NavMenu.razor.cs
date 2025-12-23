using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.UI.Navigation.Models;

namespace WebfrontCore.Components.UI.Layout;

public partial class NavMenu
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IZeroJsInterop JS { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    private NavigationInfo? NavData;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            NavData = await DataService.GetNavigationDataAsync();
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

    private string Loc(string key)
    {
        return AppState.Loc(key);
    }
    
}
