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

    // Default true — this is the share page, hiding the drop-ins by default would
    // silently misrepresent the run (a 4-player match showing as 3). User can flip
    // to qualified-only via the same toggle the leaderboard uses, just inverted
    // starting state. State lives here (not in the scrubber) so the player tab bar
    // and timeline lanes update from one source.
    private bool _showAllPlayers = true;
    private void ToggleShowAllPlayers() => _showAllPlayers = !_showAllPlayers;

    // Expansion state for nested EE step parents (e.g. Castle bows: a single bow
    // upgrade with its ritual sub-steps). Keyed by "{quest.Id}|{step.Key}" so the
    // same parent step in different quests can't collide. Default collapsed; the
    // render auto-expands parents that are mid-progress so partial state stays
    // visible without a click.
    private readonly HashSet<string> _expandedEeSteps = new(StringComparer.Ordinal);

    private static string EeStepExpansionKey(string questId, string stepKey) =>
        string.Concat(questId, "|", stepKey);

    private void ToggleEeStep(string questId, string stepKey)
    {
        var key = EeStepExpansionKey(questId, stepKey);
        if (!_expandedEeSteps.Add(key))
        {
            _expandedEeSteps.Remove(key);
        }
    }

    private bool IsEeStepExpanded(string questId, EasterEggStepInventoryEntry step,
        Dictionary<string, EasterEggStepRecord> stepsByKey)
    {
        if (_expandedEeSteps.Contains(EeStepExpansionKey(questId, step.Key))) return true;
        // Auto-expand mid-progress parents so the user sees what's pending without
        // having to click. Fully-done and not-yet-started stay collapsed.
        var leaves = step.Leaves().ToList();
        var fired = leaves.Count(l => stepsByKey.ContainsKey(l.Key));
        return fired > 0 && fired < leaves.Count;
    }

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
