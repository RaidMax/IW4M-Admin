using Data.Models;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Home.Pages;

public partial class Home
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [SupplyParameterFromQuery] public string? Game { get; set; }

    private IW4MAdminInfo? Model;

    private int OccupancyPercent => Model is null || Model.TotalAvailableClientSlots <= 0
        ? 0
        : Math.Clamp((int)Math.Round(100.0 * Model.TotalOccupiedClientSlots / Model.TotalAvailableClientSlots), 0, 100);

    protected override async Task OnParametersSetAsync()
    {
        Reference.Game? gameEnum = null;
        if (!string.IsNullOrEmpty(Game) && Enum.TryParse<Reference.Game>(Game, out var g))
        {
            gameEnum = g;
        }

        Model = await DataService.GetStatusAsync(gameEnum);
    }

    private static string TabClass(bool active) =>
        "px-3 py-1.5 rounded-md text-sm font-medium whitespace-nowrap transition-colors " +
        (active
            ? "bg-primary text-background font-semibold shadow-sm"
            : "text-subtle hover:text-foreground hover:bg-surface-hover");

    /// <summary>
    /// Safe localization that returns a fallback if AppState isn't ready.
    /// </summary>
    private string GetLocalizedString(string key)
    {
        try
        {
            var result = AppState?.Loc(key);
            return string.IsNullOrEmpty(result) || result == key ? "Server Overview" : result;
        }
        catch
        {
            return "Server Overview";
        }
    }

    /// <summary>
    /// Gets OpenGraph description with real data when available.
    /// </summary>
    private string GetOpenGraphDescription()
    {
        if (Model == null)
            return "View server status and player information";

        var total = Model.TotalClientCount.ToString("#,##0");
        var recent = Model.RecentClientCount.ToString("#,##0");
        return $"{total} total players • {recent} recently active";
    }
}
