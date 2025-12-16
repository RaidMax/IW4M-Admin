using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.JSInterop;
using WebfrontCore.QueryHelpers.Models;
using WebfrontCore.Services;
using WebfrontCore.Permissions;

namespace WebfrontCore.Components.Pages.Client;

public partial class AdvancedFind
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required IZeroJsInterop JsInterop { get; set; }
    [Inject] public required Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider AuthProvider { get; set; }
    [Inject] public required Microsoft.AspNetCore.Authorization.IAuthorizationService AuthService { get; set; }

    private List<ClientResourceResponse> Results { get; set; } = new();
    private ClientResourceRequest Request { get; set; } = new();
    private int Offset { get; set; } = 0;
    private const int Count = 30;
    private bool HasMoreResults { get; set; } = true;
    private bool _isLoading = false;
    private ElementReference _loadMoreTrigger;
    private DotNetObjectReference<AdvancedFind> _dotNetRef;
    private bool CanSeeIp { get; set; }
    private bool CanSeeLevel { get; set; }
    private bool _observerSetup;

    protected override async Task OnInitializedAsync()
    {
        NavManager.LocationChanged += OnLocationChanged;
        await LoadSearchParameters();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (HasMoreResults && !_observerSetup)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JsInterop.SetupInfiniteScroll(_loadMoreTrigger, _dotNetRef);
            _observerSetup = true;
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

    private async Task LoadData()
    {
        if (_isLoading) return;

        _isLoading = true;
        StateHasChanged();

        try
        {
            Request.Offset = Offset;
            Request.Count = Count;

            var response = await Api.GetClientsAsync(Request);
            if (response != null && response.Any())
            {
                Results.AddRange(response);
                if (response.Count() < Count)
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
            System.Console.WriteLine($"Error loading clients: {ex.Message}");
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
        NavManager.LocationChanged -= OnLocationChanged;
        _dotNetRef?.Dispose();
        await Task.CompletedTask;
    }

    private async void OnLocationChanged(object sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        await LoadSearchParameters();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadSearchParameters()
    {
        // Reset state
        Results.Clear();
        Offset = 0;
        HasMoreResults = true;
        _observerSetup = false;

        // Parse query parameters
        var uri = new Uri(NavManager.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        Request = new ClientResourceRequest();
        Request.ClientName = query["clientName"];
        Request.IsExactClientName = bool.Parse(query["isExactClientName"] ?? "false");
        Request.ClientIp = query["clientIP"];
        Request.IsExactClientIp = bool.Parse(query["isExactClientIP"] ?? "false");
        Request.ClientGuid = query["clientGuid"];

        if (Enum.TryParse<EFClient.Permission>(query["clientLevel"], out var level))
            Request.ClientLevel = level;

        if (Enum.TryParse<Reference.Game>(query["gameName"], out var game))
            Request.GameName = game;

        if (DateTime.TryParse(query["clientConnected"], out var connected))
            Request.ClientConnected = connected;
        if (int.TryParse(query["direction"], out var direction))
            Request.Direction = (SharedLibraryCore.Dtos.SortDirection)direction;

        Request.SortColumn = query["sortColumn"];

        Request.RequesterPermission = AppState.User?.Level ?? EFClient.Permission.User;

        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var canReadIp = (await AuthService.AuthorizeAsync(user, $"Permissions.{WebfrontEntity.ClientIPAddress}.{WebfrontPermission.Read}")).Succeeded;
        var canReadGuid = (await AuthService.AuthorizeAsync(user, $"Permissions.{WebfrontEntity.ClientGuid}.{WebfrontPermission.Read}")).Succeeded;
        var canReadLevel = (await AuthService.AuthorizeAsync(user, $"Permissions.{WebfrontEntity.ClientLevel}.{WebfrontPermission.Read}")).Succeeded;

        if (!canReadIp)
        {
            Request.ClientIp = null;
            Request.IsExactClientIp = false;
        }

        if (!canReadGuid)
        {
            Request.ClientGuid = null;
        }

        CanSeeIp = canReadIp;
        CanSeeLevel = canReadLevel;

        await LoadData();
    }

    private string MakeAbbreviation(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var words = text.Split(' ');
        if (words.Length == 1) return text;

        return string.Join("", words.Select(w => w.Length > 0 ? w[0].ToString() : ""));
    }

    private string FormatIp(int? ip) => ip.HasValue ? SharedLibraryCore.Utilities.ConvertIPtoString(ip.Value) : "-";
}
