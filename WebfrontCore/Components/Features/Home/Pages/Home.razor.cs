using Data.Models;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Home.Pages;

public partial class Home
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
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
}
