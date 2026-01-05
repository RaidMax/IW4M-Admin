using Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebfrontCore.Core.Services;
using PenaltyInfo = SharedLibraryCore.Dtos.PenaltyInfo;

namespace WebfrontCore.Components.Features.Penalties.Pages;

public partial class PenaltyList
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required ILogger<PenaltyList> Logger { get; set; }
    private List<PenaltyInfo> Penalties { get; set; } = [];
    private int Offset { get; set; } = 0;
    private int Count { get; set; } = 30;
    private bool IgnoreAutomated { get; set; } = true;
    private EFPenalty.PenaltyType ShowOnly { get; set; } = EFPenalty.PenaltyType.Any;
    private bool HasMoreResults { get; set; } = true;
    private long _totalCount;

    private bool _isLoading = false;

    // Removed unused _loadMoreTrigger
    private DotNetObjectReference<PenaltyList>? _dotNetRef;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            // Use the new global infinite scroll helper
            await JS.InvokeVoidAsync("window.infiniteScroll.initialize", _dotNetRef, "loadMoreTrigger");
        }
    }

    [JSInvokable]
    public async Task LoadMore()
    {
        if (!HasMoreResults || _isLoading)
        {
            return;
        }

        Offset += Count;
        await LoadData();
        StateHasChanged();
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
        Offset = 0;
        Penalties.Clear();
        HasMoreResults = true;
        _totalCount = 0;
        StateHasChanged();
        await LoadData();
    }

    private async Task LoadData()
    {
        if (_isLoading) return;

        _isLoading = true;
        StateHasChanged();

        try
        {
            var request = new Controllers.API.Models.PenaltyRequest
            {
                Offset = Offset,
                Count = Count,
                ShowOnly = ShowOnly,
                IgnoreAutomated = IgnoreAutomated
            };

            if (Offset == 0)
            {
                _totalCount = await DataService.GetPenaltiesCountAsync(request);
            }

            var result = await DataService.GetPenaltiesAsync(request);
            if (result != null && result.Any())
            {
                Penalties.AddRange(result);
                if (result.Count < Count)
                {
                    HasMoreResults = false;
                }
            }
            else
            {
                HasMoreResults = false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading penalties");
            HasMoreResults = false;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Disconnect infinite scroll - wrapped in try-catch because disposal can 
        // occur during static rendering when JS interop is unavailable
        try
        {
            await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during static rendering - safe to ignore
        }

        _dotNetRef?.Dispose();
    }
}
