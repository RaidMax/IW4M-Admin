using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using WebfrontCore.Core.Services;
using WebfrontCore.Components.UI.Navigation.Models;

namespace WebfrontCore.Components.UI.Layout;

public partial class NavMenu : IDisposable
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IActionService ActionService { get; set; }

    /// <summary>
    /// When true the menu renders as an icon-only rail (labels hidden, tooltips via title).
    /// </summary>
    [Parameter] public bool Collapsed { get; set; }

    /// <summary>
    /// Persisted navigation data that survives SSR-to-interactive handoff during enhanced navigation.
    /// </summary>
    [PersistentState(AllowUpdates = true)]
    public NavigationInfo? NavData { get; set; }

    private string BrandInitial
    {
        get
        {
            var branding = AppState.WebfrontBranding.StripColors().Trim();
            return string.IsNullOrEmpty(branding) ? "I" : branding[..1].ToUpperInvariant();
        }
    }

    private string UserInitial
    {
        get
        {
            var name = AppState.User?.Name?.StripColors().Trim();
            return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        // If NavData was restored from persistent state, just apply it
        if (NavData is not null)
        {
            ApplyNavData();
            AppState.OnChange += StateHasChanged;
            return;
        }

        // First load - fetch navigation data
        try
        {
            NavData = await DataService.GetNavigationDataAsync();
            ApplyNavData();
        }
        catch
        {
            // Handle error (offline/api fail)
        }

        AppState.OnChange += StateHasChanged;
    }

    private void ApplyNavData()
    {
        if (NavData?.User != null)
        {
            AppState.InitializeUser(NavData.User);
        }
    }

    public void Dispose()
    {
        AppState.OnChange -= StateHasChanged;
    }

    private string Loc(string key)
    {
        return AppState.Loc(key);
    }

    private void CloseMobileMenu()
    {
        if (AppState.IsMobileNavOpen)
        {
            AppState.IsMobileNavOpen = false;
        }
    }
}
