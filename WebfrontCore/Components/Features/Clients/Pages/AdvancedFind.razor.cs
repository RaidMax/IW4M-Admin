using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;

namespace WebfrontCore.Components.Features.Clients.Pages;

public partial class AdvancedFind
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required ApplicationConfiguration AppConfig { get; set; }

    [SupplyParameterFromQuery(Name = "clientName")]
    public string? ClientName { get; set; }

    [SupplyParameterFromQuery(Name = "isExactClientName")]
    public bool IsExactClientName { get; set; }

    [SupplyParameterFromQuery(Name = "clientIP")]
    public string? ClientIp { get; set; }

    [SupplyParameterFromQuery(Name = "isExactClientIP")]
    public bool IsExactClientIp { get; set; }

    [SupplyParameterFromQuery(Name = "clientGuid")]
    public string? ClientGuid { get; set; }

    [SupplyParameterFromQuery(Name = "clientLevel")]
    public string? ClientLevel { get; set; }

    [SupplyParameterFromQuery(Name = "gameName")]
    public string? GameName { get; set; }

    [SupplyParameterFromQuery(Name = "clientConnected")]
    public DateTime? ClientConnected { get; set; }

    [SupplyParameterFromQuery(Name = "direction")]
    public int? Direction { get; set; }

    [SupplyParameterFromQuery(Name = "sortColumn")]
    public string? SortColumn { get; set; }

    private List<ClientResourceResponse> Results { get; } = [];
    private int _offset;
    private const int PageSize = 30;
    private bool _hasMore = true;
    private bool _isLoading;
    private long _totalCount;

    protected override async Task OnParametersSetAsync()
    {
        ResetState();
        await LoadDataAsync();
    }

    [Inject] public required IJSRuntime JS { get; set; }
    private DotNetObjectReference<AdvancedFind>? _dotNetRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("window.infiniteScroll.initialize", _dotNetRef, "loadMoreTrigger");
        }
    }

    [JSInvokable]
    public async Task LoadMore()
    {
        if (_isLoading || !_hasMore)
            return;

        _offset += PageSize;
        await LoadDataAsync();
        StateHasChanged();
    }

    private void ResetState()
    {
        Results.Clear();
        _offset = 0;
        _hasMore = true;
        _totalCount = 0;
    }

    private ClientResourceRequest BuildRequest()
    {
        var request = new ClientResourceRequest
        {
            ClientName = ClientName,
            IsExactClientName = IsExactClientName,
            ClientIp = ClientIp,
            IsExactClientIp = IsExactClientIp,
            ClientGuid = ClientGuid,
            ClientConnected = ClientConnected,
            SortColumn = SortColumn,
            Offset = _offset,
            Count = PageSize
        };

        if (Enum.TryParse<EFClient.Permission>(ClientLevel, out var level))
            request.ClientLevel = level;

        if (Enum.TryParse<Reference.Game>(GameName, out var game))
            request.GameName = game;

        if (Direction.HasValue)
            request.Direction = (SortDirection)Direction.Value;

        return request;
    }

    private string? _validationError;

    private async Task LoadDataAsync()
    {
        if (_isLoading)
            return;

        _validationError = null;
        
        if (!string.IsNullOrWhiteSpace(ClientName) && ClientName.Length < AppConfig.MinimumNameLength)
        {
            _validationError = AppState.Loc("WEBFRONT_SEARCH_LENGTH_ERROR").FormatExt(AppConfig.MinimumNameLength);
            _hasMore = false;
            Results.Clear();
            StateHasChanged();
            return;
        }

        _isLoading = true;
        StateHasChanged();

        try
        {
            var response = await DataService.SearchClientsAsync(BuildRequest());
            var results = response.Results.ToList();

            if (_offset == 0)
            {
                _totalCount = response.TotalResultCount;
            }

            if (results.Count > 0)
            {
                Results.AddRange(results);
            }

            _hasMore = results.Count >= PageSize;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading clients: {ex.Message}");
            _hasMore = false;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }


    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");
        }
        catch (JSDisconnectedException)
        {
            // Allowed
        }

        _dotNetRef?.Dispose();
    }
}
