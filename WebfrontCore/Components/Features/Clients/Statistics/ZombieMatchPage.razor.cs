using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieMatchPage
{
    [Parameter] public int MatchId { get; set; }
    [Parameter] public int? ClientId { get; set; }

    [Inject] public required IJSRuntime JS { get; set; }

    /// <summary>
    /// Persisted state — survives SSR-to-interactive handoff so the OG meta tags
    /// land in the initial server-rendered HTML (bots don't run JS).
    /// </summary>
    [PersistentState(AllowUpdates = true)]
    public MatchPageState? State { get; set; }

    private SharedLibraryCore.Interfaces.ZombieMatchDetail? Detail => State?.Detail;
    private bool _premiumMissing;
    private bool _loading;
    private bool _copied;
    private int _lastLoadedId;

    private string _ogTitle => Detail is null
        ? AppState.Loc("WEBFRONT_ZOMBIE_MATCH_LOADING")
        : AppState.Loc("WEBFRONT_ZOMBIE_MATCH_OG_TITLE").FormatExt(Detail.HighestRound, Detail.Map);

    private string _ogDescription => Detail is null
        ? AppState.Loc("WEBFRONT_ZOMBIE_MATCH_LOADING")
        : AppState.Loc("WEBFRONT_ZOMBIE_MATCH_OG_DESCRIPTION").FormatExt(
            Detail.Players.Count,
            Detail.DurationMinutes.ToString("F0"),
            Detail.Date.UtcDateTime.ToStandardFormat());

    protected override async Task OnParametersSetAsync()
    {
        // Skip refetch when state was hydrated from SSR for the same match.
        if (Detail is not null && Detail.MatchId == MatchId)
        {
            _lastLoadedId = MatchId;
            return;
        }

        var matchHistoryService = ServiceProvider.GetService(typeof(IZombieMatchHistoryService)) as IZombieMatchHistoryService;
        if (matchHistoryService is null)
        {
            _premiumMissing = true;
            return;
        }

        _loading = true;
        try
        {
            State ??= new MatchPageState();
            State.Detail = await matchHistoryService.GetMatchDetailAsync(MatchId);
            _lastLoadedId = MatchId;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task CopyShareLink()
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", NavManager.Uri);
            _copied = true;
            StateHasChanged();
            await Task.Delay(2000);
            _copied = false;
            StateHasChanged();
        }
        catch
        {
            // Clipboard API requires HTTPS or localhost — fail silently
        }
    }

    public class MatchPageState
    {
        public SharedLibraryCore.Interfaces.ZombieMatchDetail? Detail { get; set; }
    }
}
