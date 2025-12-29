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
    private List<PenaltyInfo> Penalties { get; set; } = [];
    private int Offset { get; set; } = 0;
    private int Count { get; set; } = 30;
    private bool IgnoreAutomated { get; set; } = true;
    private EFPenalty.PenaltyType ShowOnly { get; set; } = EFPenalty.PenaltyType.Any;
    private bool HasMoreResults { get; set; } = true;

    private bool _isLoading = false;

    // Removed unused _loadMoreTrigger
    private DotNetObjectReference<PenaltyList> _dotNetRef;

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
            var result = await DataService.GetPenaltiesAsync(new WebfrontCore.Controllers.API.Models.PenaltyRequest
            {
                Offset = Offset,
                Count = Count,
                ShowOnly = ShowOnly,
                IgnoreAutomated = IgnoreAutomated
            });
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
            System.Console.WriteLine($"Error loading penalties: {ex.Message}");
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
        // Disconnect infinite scroll
        await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");

        _dotNetRef?.Dispose();
    }
}
