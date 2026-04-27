using Data.Models;
using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;
using PenaltyInfo = SharedLibraryCore.Dtos.PenaltyInfo;

namespace WebfrontCore.Components.Features.Penalties.Pages;

public partial class PenaltyList
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required ILogger<PenaltyList> Logger { get; set; }

    /// <summary>
    /// Persisted state that survives SSR-to-interactive handoff during enhanced navigation.
    /// </summary>
    [PersistentState(AllowUpdates = true)]
    public PenaltyListState? State { get; set; }

    private int Count { get; set; } = 30;
    private bool IgnoreAutomated { get; set; } = true;
    private EFPenalty.PenaltyType ShowOnly { get; set; } = EFPenalty.PenaltyType.Any;
    private bool _isLoading;

    protected override async Task OnInitializedAsync()
    {
        // If State was restored from persistent state, skip loading
        if (State is not null)
        {
            Logger.LogDebug("PenaltyList: State restored from persistent state ({Count} items)", State.Penalties.Count);
            return;
        }

        // First load - initialize state and fetch data
        Logger.LogDebug("PenaltyList: Initializing fresh state");
        State = new PenaltyListState();
        await LoadData();
    }

    private async Task LoadMore()
    {
        if (!(State?.HasMoreResults ?? false) || _isLoading)
        {
            return;
        }

        State.Offset += Count;
        await LoadData();
    }

    private async Task OnIgnoreAutomatedChanged(ChangeEventArgs e)
    {
        IgnoreAutomated = (bool)e.Value!;
        await ResetAndLoad();
    }

    private async Task OnShowOnlyChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var val))
        {
            ShowOnly = (EFPenalty.PenaltyType)val;
            await ResetAndLoad();
        }
    }

    private async Task ResetAndLoad()
    {
        State = new PenaltyListState();
        StateHasChanged();
        await LoadData();
    }

    private async Task LoadData()
    {
        if (_isLoading || State is null)
            return;

        _isLoading = true;
        StateHasChanged();

        try
        {
            var request = new Controllers.API.Models.PenaltyRequest
            {
                Offset = State.Offset,
                Count = Count,
                ShowOnly = ShowOnly,
                IgnoreAutomated = IgnoreAutomated
            };

            if (State.Offset == 0)
            {
                State.TotalCount = await DataService.GetPenaltiesCountAsync(request);
            }

            var result = await DataService.GetPenaltiesAsync(request);
            if (result is { Count: > 0 })
            {
                State.Penalties.AddRange(result);
                if (result.Count < Count)
                {
                    State.HasMoreResults = false;
                }
            }
            else
            {
                State.HasMoreResults = false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading penalties");
            State.HasMoreResults = false;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// State class for persistent state serialization during SSR-to-interactive handoff.
    /// All display-relevant values are stored here to prevent flashing during enhanced navigation.
    /// </summary>
    public class PenaltyListState
    {
        public List<PenaltyInfo> Penalties { get; set; } = [];
        public int Offset { get; set; }
        public long TotalCount { get; set; }
        public bool HasMoreResults { get; set; } = true;
    }
}
