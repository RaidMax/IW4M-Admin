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
    [Inject] public required ILogger<AdvancedFind> Logger { get; set; }

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

    [PersistentState(AllowUpdates = true)]
    public AdvancedFindState? State { get; set; }

    private const int PageSize = 30;

    private bool _isLoading;
    private List<ClientResourceResponse> Results => State?.Results ?? [];
    private bool _hasMore => State?.HasMore ?? false;
    private long _totalCount => State?.TotalCount ?? 0;
    private string? _validationError => State?.ValidationError;

    protected override async Task OnParametersSetAsync()
    {
        // Initialize state if not restored
        if (State is null)
        {
            State = new AdvancedFindState();
            SetStateParams(State);
            ResetState();
            await LoadDataAsync();
        }
        else
        {
            // Check if restored state matches current parameters AND has valid data
            // We consider state valid if params match AND either:
            // - We have results loaded, OR
            // - TotalCount is set (meaning we completed a search, possibly with 0 results)
            var hasValidData = State.Results.Count > 0 || State.TotalCount > 0 || State.ValidationError != null;
            
            if (IsStateMatch(State) && hasValidData)
            {
                // Restored state is valid for current params with data, skip load
            }
            else
            {
                // Params changed or no valid data, reset and reload
                SetStateParams(State);
                ResetState();
                await LoadDataAsync();
            }
        }
    }

    private void SetStateParams(AdvancedFindState state)
    {
        state.ClientName = ClientName;
        state.IsExactClientName = IsExactClientName;
        state.ClientIp = ClientIp;
        state.IsExactClientIp = IsExactClientIp;
        state.ClientGuid = ClientGuid;
        state.ClientLevel = ClientLevel;
        state.GameName = GameName;
        state.ClientConnected = ClientConnected;
        state.Direction = Direction;
        state.SortColumn = SortColumn;
    }

    private bool IsStateMatch(AdvancedFindState state)
    {
        return state.ClientName == ClientName &&
               state.IsExactClientName == IsExactClientName &&
               state.ClientIp == ClientIp &&
               state.IsExactClientIp == IsExactClientIp &&
               state.ClientGuid == ClientGuid &&
               state.ClientLevel == ClientLevel &&
               state.GameName == GameName &&
               state.ClientConnected == ClientConnected &&
               state.Direction == Direction &&
               state.SortColumn == SortColumn;
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
        if (_isLoading || !_hasMore || State == null)
            return;

        State.Offset += PageSize;
        await LoadDataAsync();
        StateHasChanged();
    }

    private void ResetState()
    {
        if (State == null) return;
        State.Results.Clear();
        State.Offset = 0;
        State.HasMore = true;
        State.TotalCount = 0;
        State.ValidationError = null;
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
            Offset = State?.Offset ?? 0,
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

    private async Task LoadDataAsync()
    {
        if (_isLoading || State == null)
            return;

        State.ValidationError = null;

        if (!string.IsNullOrWhiteSpace(ClientName) && ClientName.Length < AppConfig.MinimumNameLength)
        {
            State.ValidationError = AppState.Loc("WEBFRONT_SEARCH_LENGTH_ERROR").FormatExt(AppConfig.MinimumNameLength);
            State.HasMore = false;
            State.Results.Clear();
            StateHasChanged();
            return;
        }

        _isLoading = true;
        StateHasChanged();

        try
        {
            var response = await DataService.SearchClientsAsync(BuildRequest());
            var results = response.Results.ToList();

            if (State.Offset == 0 && response.TotalResultCount > 0)
            {
                State.TotalCount = response.TotalResultCount;
            }

            if (results.Count > 0)
            {
                State.Results.AddRange(results);
            }

            State.HasMore = results.Count >= PageSize;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading clients");
            State.HasMore = false;
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
        catch (InvalidOperationException)
        {
            // Allowed
        }
        catch (JSDisconnectedException)
        {
            // Allowed
        }

        _dotNetRef?.Dispose();
    }
    
    public class AdvancedFindState
    {
        public List<ClientResourceResponse> Results { get; set; } = [];
        public int Offset { get; set; }
        public bool HasMore { get; set; } = true;
        public long TotalCount { get; set; }
        public string? ValidationError { get; set; }
        
        // Params Snapshot
        public string? ClientName { get; set; }
        public bool IsExactClientName { get; set; }
        public string? ClientIp { get; set; }
        public bool IsExactClientIp { get; set; }
        public string? ClientGuid { get; set; }
        public string? ClientLevel { get; set; }
        public string? GameName { get; set; }
        public DateTime? ClientConnected { get; set; }
        public int? Direction { get; set; }
        public string? SortColumn { get; set; }
    }
}
