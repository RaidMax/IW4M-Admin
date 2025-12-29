using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Configuration;
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

    private List<MessageResponse> Results { get; } = [];
    private int _offset;
    private const int PageSize = 30;
    private bool _hasMore = true;
    private bool _isLoading;
    private DotNetObjectReference<FindMessage>? _dotNetRef;

    // Context modal state
    private bool _showContextModal;
    private MessageResponse? _selectedMessage;
    private List<MessageResponse>? _contextMessages;
    private bool _contextLoading;

    protected override async Task OnParametersSetAsync()
    {
        ResetState();
        await LoadDataAsync();
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
    }

    private ChatSearchQuery BuildRequest()
    {
        var request = new ChatSearchQuery
        {
            MessageContains = MessageContains,
            IsExactMatch = IsExactMatch,
            ClientId = ClientId,
            ServerId = ServerId,
            Offset = _offset,
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
        if (_isLoading)
            return;

        _isLoading = true;
        StateHasChanged();

        try
        {
            var response = await DataService.SearchMessagesAsync(BuildRequest());

            if (response.RetrievedResultCount > 0)
            {
                Results.AddRange(response.Results);
            }

            _hasMore = response.RetrievedResultCount >= PageSize;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading messages: {ex.Message}");
            _hasMore = false;
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
            System.Console.WriteLine($"Error loading chat context: {ex.Message}");
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
}
