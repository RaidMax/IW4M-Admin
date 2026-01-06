using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Dtos.Meta.Responses;
using Stats.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Search.Pages;

public partial class FindMessage : IAsyncDisposable
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required ILogger<FindMessage> Logger { get; set; }

    [SupplyParameterFromQuery(Name = "messageContains")]
    public string? MessageContains { get; set; }

    [SupplyParameterFromQuery(Name = "isExactMatch")]
    public bool IsExactMatch { get; set; }

    [SupplyParameterFromQuery(Name = "clientId")]
    public int? ClientId { get; set; }

    [SupplyParameterFromQuery(Name = "serverId")]
    public string? ServerId { get; set; }

    [SupplyParameterFromQuery(Name = "sentAfter")]
    public DateTime? SentAfter { get; set; }

    [SupplyParameterFromQuery(Name = "sentAfterTime")]
    public string? SentAfterTime { get; set; }

    [SupplyParameterFromQuery(Name = "sentBefore")]
    public DateTime? SentBefore { get; set; }

    [SupplyParameterFromQuery(Name = "sentBeforeTime")]
    public string? SentBeforeTime { get; set; }

    [SupplyParameterFromQuery(Name = "direction")]
    public int? Direction { get; set; }

    [PersistentState(AllowUpdates = true)]
    public FindMessageState? State { get; set; }

    private const int PageSize = 30;

    private bool _isLoading;
    private List<MessageResponse> Results => State?.Results ?? [];
    private bool _hasMore => State?.HasMore ?? false;
    private long _totalCount => State?.TotalCount ?? 0;

    private DotNetObjectReference<FindMessage>? _dotNetRef;

    // Context modal state (not persisted - ephemeral UI state)
    private bool _showContextModal;
    private MessageResponse? _selectedMessage;
    private List<MessageResponse>? _contextMessages;
    private bool _contextLoading;

    protected override async Task OnParametersSetAsync()
    {
        // Initialize state if not restored
        if (State is null)
        {
            State = new FindMessageState();
            SetStateParams(State);
            ResetState();
            await LoadDataAsync();
        }
        else
        {
            // Check if restored state matches current parameters AND has valid data
            var hasValidData = State.Results.Count > 0 || State.TotalCount > 0;

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

    private void SetStateParams(FindMessageState state)
    {
        state.MessageContains = MessageContains;
        state.IsExactMatch = IsExactMatch;
        state.ClientId = ClientId;
        state.ServerId = ServerId;
        state.SentAfter = SentAfter;
        state.SentAfterTime = SentAfterTime;
        state.SentBefore = SentBefore;
        state.SentBeforeTime = SentBeforeTime;
        state.Direction = Direction;
    }

    private bool IsStateMatch(FindMessageState state)
    {
        return state.MessageContains == MessageContains &&
               state.IsExactMatch == IsExactMatch &&
               state.ClientId == ClientId &&
               state.ServerId == ServerId &&
               state.SentAfter == SentAfter &&
               state.SentAfterTime == SentAfterTime &&
               state.SentBefore == SentBefore &&
               state.SentBeforeTime == SentBeforeTime &&
               state.Direction == Direction;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("window.infiniteScroll.initialize", _dotNetRef, "loadMoreMessageTrigger");
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
    }

    private ChatSearchQuery BuildRequest()
    {
        var request = new ChatSearchQuery
        {
            MessageContains = MessageContains,
            IsExactMatch = IsExactMatch,
            ClientId = ClientId,
            ServerId = ServerId,
            Offset = State?.Offset ?? 0,
            Count = PageSize,
            Direction = Direction.HasValue ? (SortDirection)Direction.Value : SortDirection.Descending
        };

        // Parse date and time for SentAfter
        if (SentAfter.HasValue)
        {
            request.SentAfter = SentAfter.Value.Date;
            request.SentAfterTime = SentAfterTime;
        }

        // Parse date and time for SentBefore
        if (SentBefore.HasValue)
        {
            request.SentBefore = SentBefore.Value.Date;
            request.SentBeforeTime = SentBeforeTime;
        }
        else
        {
            request.SentBefore = DateTime.UtcNow;
        }

        return request;
    }

    private async Task LoadDataAsync()
    {
        if (_isLoading || State == null)
            return;

        _isLoading = true;
        StateHasChanged();

        try
        {
            var response = await DataService.SearchMessagesAsync(BuildRequest());

            if (response.RetrievedResultCount > 0)
            {
                State.Results.AddRange(response.Results);
            }

            if (response.TotalResultCount > 0)
            {
                State.TotalCount = response.TotalResultCount;
            }

            State.HasMore = response.RetrievedResultCount >= PageSize;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading messages");
            State.HasMore = false;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OpenContextModal(MessageResponse message)
    {
        _selectedMessage = message;
        _showContextModal = true;
        _contextMessages = null;
        _contextLoading = true;
        StateHasChanged();

        try
        {
            _contextMessages = await DataService.GetChatContextAsync(
                message.ServerId.ToString(),
                message.When.ToFileTimeUtc());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading chat context for message at {Timestamp}", message.When);
        }
        finally
        {
            _contextLoading = false;
            StateHasChanged();
        }
    }

    private void CloseContextModal()
    {
        _showContextModal = false;
        _selectedMessage = null;
        _contextMessages = null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected, allowed
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during static prerendering, allowed
        }

        _dotNetRef?.Dispose();
    }

    public class FindMessageState
    {
        public List<MessageResponse> Results { get; set; } = [];
        public int Offset { get; set; }
        public bool HasMore { get; set; } = true;
        public long TotalCount { get; set; }

        // Params Snapshot
        public string? MessageContains { get; set; }
        public bool IsExactMatch { get; set; }
        public int? ClientId { get; set; }
        public string? ServerId { get; set; }
        public DateTime? SentAfter { get; set; }
        public string? SentAfterTime { get; set; }
        public DateTime? SentBefore { get; set; }
        public string? SentBeforeTime { get; set; }
        public int? Direction { get; set; }
    }
}
