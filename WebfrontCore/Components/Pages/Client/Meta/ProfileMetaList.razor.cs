using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Pages.Client.Meta;

public partial class ProfileMetaList
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IZeroJsInterop JsInterop { get; set; }
    [Parameter] public int ClientId { get; set; }
    [Parameter] public MetaType? MetaFilterType { get; set; }
    private List<BaseMetaResponse> MetaItems { get; set; } = new();
    private bool Loading { get; set; }
    private string _errorMessage;
    private int Offset { get; set; } = 0;
    private int Count { get; set; } = 30;
    private long? StartAt { get; set; }
    private bool HasMore { get; set; } = true;
    private int _previousClientId;
    private MetaType? _previousMetaFilter;
    private DotNetObjectReference<ProfileMetaList> _objRef;
    private bool _observerSetup;
    private ElementReference _loadMoreTrigger;

    protected override async Task OnParametersSetAsync()
    {
        // Reset when client ID or filter type changes
        if (_previousClientId != ClientId || _previousMetaFilter != MetaFilterType)
        {
            MetaItems.Clear();
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
        if (MetaItems.Any() && HasMore && !_observerSetup)
        {
            _objRef = DotNetObjectReference.Create(this);
            await JsInterop.SetupInfiniteScroll(_loadMoreTrigger, _objRef);
            _observerSetup = true;
        }
    }

    private async Task LoadData()
    {
        if (Loading || !HasMore) return;
        Loading = true;
        StateHasChanged();

        try
        {
            if (MetaItems.Any())
            {
                StartAt = MetaItems.Last().When.ToFileTimeUtc();
                Offset = 0;
            }

            var newItems = await Api.GetClientMetaAsync(ClientId, Count, Offset, StartAt, MetaFilterType);
            var itemList = newItems?.ToList() ?? new List<BaseMetaResponse>();


            // Filter out duplicates (backend provider issue safeguard)
            var n = 0;
            var uniqueItems = new List<BaseMetaResponse>();

            foreach (var item in itemList)
            {
                // Simple duplicate check based on ID if available, or Type+When+Value as fallback
                // BaseMetaResponse has MetaId, but it might be 0 for some dynamic types.
                if (item.MetaId > 0)
                {
                    if (MetaItems.Any(existing => existing.MetaId == item.MetaId && existing.Type == item.Type))
                        continue;
                }
                else
                {
                    // Fallback for items without ID
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
                // If we got items but they were all duplicates, we've reached the end/loop
                HasMore = false;
            }
            else
            {
                MetaItems.AddRange(uniqueItems);
            }

            if (itemList.Count < Count)
            {
                HasMore = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading meta: {ex.Message}";
            System.Console.WriteLine($"Error loading meta: {ex}");
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
        _objRef?.Dispose();
        await Task.CompletedTask;
    }
}
