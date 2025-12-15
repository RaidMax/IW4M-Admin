using Data.Models;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Pages.Home;

public partial class Index
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [SupplyParameterFromQuery] public string Game { get; set; }

    private IW4MAdminInfo Model;
    private List<ServerInfo> Servers;

    protected override async Task OnParametersSetAsync()
    {
        Reference.Game? gameEnum = null;
        if (!string.IsNullOrEmpty(Game) && Enum.TryParse<Reference.Game>(Game, out var g))
        {
            gameEnum = g;
        }

        Model = await Api.GetStatusAsync(gameEnum);
        Servers = await Api.GetServersAsync(gameEnum);
    }

    private string FormatTranslation(string translationKey, params object[] values)
    {
        // Simple logic mirroring view
        var translation = AppState.Loc(translationKey);
        // We assume logic: "A::B" -> <span>A (formatted)</span><span>B</span>
        // But AppState.Loc returns simple string if not found.
        if (translation == translationKey) return translationKey;

        var split = translation.Split("::");
        // Using Utilities.FormatExt logic? System.String.Format?
        // Assuming standard format for now.
        return split.Length == 2
            ? $"<span class='font-weight-bold text-primary'>{split[0].FormatExt(values)}</span><span>{split[1]}</span>"
            : translation;
    }
}
