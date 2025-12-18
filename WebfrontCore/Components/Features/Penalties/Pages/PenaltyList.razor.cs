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
    private List<PenaltyInfo> Penalties { get; set; } = [];
    private int Offset { get; set; } = 0;
    private int Count { get; set; } = 30;
    private bool IgnoreAutomated { get; set; } = true;
    private EFPenalty.PenaltyType ShowOnly { get; set; } = EFPenalty.PenaltyType.Any;
    private bool HasMoreResults { get; set; } = true;
    private bool _isLoading = false;
    private ElementReference _loadMoreTrigger;
    private DotNetObjectReference<PenaltyList> _dotNetRef;
    private IJSObjectReference _jsModule;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/blazor_lib.js");

            // Set up intersection observer for infinite scroll
            await JS.InvokeVoidAsync("blazorInfiniteScroll.setup", _loadMoreTrigger, _dotNetRef);
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
            var result = await DataService.GetPenaltiesAsync(Offset, Count, ShowOnly, IgnoreAutomated);
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
        if (_jsModule != null)
        {
            await _jsModule.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }
}
