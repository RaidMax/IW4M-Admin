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
    [Inject] public required ApplicationConfiguration AppConfig { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [SupplyParameterFromQuery] public string Game { get; set; }

    private IW4MAdminInfo Model;

    protected override async Task OnParametersSetAsync()
    {
        Reference.Game? gameEnum = null;
        if (!string.IsNullOrEmpty(Game) && Enum.TryParse<Reference.Game>(Game, out var g))
        {
            gameEnum = g;
        }

        Model = await DataService.GetStatusAsync(gameEnum);
    }

    private string FormatTranslation(string translationKey, params object[] values)
    {
        var translation = AppState.Loc(translationKey);
        if (translation == translationKey) return translationKey;

        var split = translation.Split("::");
        return split.Length == 2
            ? $"<span class='font-weight-bold text-primary'>{split[0].FormatExt(values)}</span><span>{split[1]}</span>"
            : translation;
    }

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
