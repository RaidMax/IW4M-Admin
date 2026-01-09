using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Components;

public partial class ClientMetaList : IAsyncDisposable
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required ILogger<ClientMetaList> Logger { get; set; }

    [Parameter] public int ClientId { get; set; }
    [Parameter] public MetaType? MetaFilterType { get; set; }

    private List<BaseMetaResponse> MetaItems { get; set; } = [];
    private bool Loading { get; set; }
    private string? _errorMessage;
    private int Offset { get; set; } = 0;
    private int Count { get; set; } = 30;
    private long? StartAt { get; set; }
    private bool HasMore { get; set; } = true;
    private int _previousClientId;
    private MetaType? _previousMetaFilter;
    private DotNetObjectReference<ClientMetaList>? _objRef;
    private bool _observerSetup;

    // State container for individual meta items (expansion, loading, extra data)
    private Dictionary<object, MetaItemState> _itemStates = new();

    private class MetaItemState
    {
        public bool IsOpen { get; set; }

        public bool IsLoading { get; set; }

        // For AdministeredPenalty
        public List<Dictionary<string, string>>? SnapshotInfo { get; set; }

        // For Message
        public List<MessageResponse>? ContextMessages { get; set; }
    }

    protected override async Task OnParametersSetAsync()
    {
        // Reset when client ID or filter type changes
        if (_previousClientId != ClientId || _previousMetaFilter != MetaFilterType)
        {
            MetaItems.Clear();
            _itemStates.Clear();
            Offset = 0;
            StartAt = DateTime.UtcNow.ToFileTimeUtc();
            HasMore = true;
            _previousClientId = ClientId;
            _previousMetaFilter = MetaFilterType;
            _observerSetup = false;
        }

        if (MetaItems.Count == 0)
        {
            await LoadData();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (MetaItems.Count != 0 && HasMore && !_observerSetup)
        {
            _objRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("window.infiniteScroll.initialize", _objRef, "loadMoreMetaTrigger");
            _observerSetup = true;
        }
    }

    private async Task LoadData()
    {
        if (Loading || !HasMore)
            return;
        Loading = true;
        StateHasChanged();

        try
        {
            if (MetaItems.Count != 0)
            {
                StartAt = MetaItems.Last().When.ToFileTimeUtc();
                Offset = 0;
            }

            var newItems = await DataService.GetClientMetaAsync(
                new WebfrontCore.Controllers.API.Models.ClientMetaRequest
                {
                    ClientId = ClientId,
                    Count = Count,
                    Offset = Offset,
                    StartAt = StartAt,
                    MetaType = MetaFilterType
                });
            var itemList = newItems?.ToList() ?? [];

            var n = 0;
            var uniqueItems = new List<BaseMetaResponse>();

            foreach (var item in itemList)
            {
                if (item.MetaId > 0)
                {
                    if (MetaItems.Any(existing => existing.MetaId == item.MetaId && existing.Type == item.Type))
                        continue;
                }
                else
                {
                    if (MetaItems.Any(existing => existing.Type == item.Type &&
                                                  existing.When == item.When &&
                                                  existing.Order == item.Order))
                        continue;
                }

                uniqueItems.Add(item);
                n++;
            }

            if (n == 0)
            {
                HasMore = false;
            }
            else
            {
                MetaItems.AddRange(uniqueItems);
            }
            
            // Note: We don't check itemList.Count < Count here because permission filtering
            // may reduce the returned count even when more items exist in the database.
            // The n == 0 check above handles the true "no more items" case.
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading meta: {ex.Message}";
            Logger.LogError(ex, "Error loading client meta for client {ClientId}", ClientId);
            HasMore = false;
        }
        finally
        {
            Loading = false;
        }
    }

    [JSInvokable]
    public async Task LoadMore()
    {
        await LoadData();
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_observerSetup)
        {
            try
            {
                await JS.InvokeVoidAsync("window.infiniteScroll.disconnect");
            }
            catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
            {
                // ignored
            }
        }

        _objRef?.Dispose();
    }

    // --- Item Logic ---

    private MetaItemState GetState(object item)
    {
        if (!_itemStates.TryGetValue(item, out var state))
        {
            state = new MetaItemState();
            _itemStates[item] = state;
        }

        return state;
    }

    public async Task TogglePenaltyDetails(AdministeredPenaltyResponse meta)
    {
        var state = GetState(meta);
        state.IsOpen = !state.IsOpen;

        if (state.IsOpen && state.SnapshotInfo == null)
        {
            state.IsLoading = true;
            try
            {
                state.SnapshotInfo = await DataService.GetAutomatedPenaltyContextAsync(meta.PenaltyId);
            }
            catch (Exception)
            {
                // Handle error silently or log
            }
            finally
            {
                state.IsLoading = false;
            }
        }
    }

    public async Task ToggleMessageContext(MessageResponse meta)
    {
        var state = GetState(meta);
        state.IsOpen = !state.IsOpen;

        if (state.IsOpen && state.ContextMessages == null)
        {
            state.IsLoading = true;
            try
            {
                state.ContextMessages =
                    await DataService.GetChatContextAsync(meta.ServerId.ToString(), meta.When.ToFileTimeUtc());
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error loading message context for message on server {ServerId}", meta.ServerId);
            }
            finally
            {
                state.IsLoading = false;
            }
        }
    }
}
