using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Pages;

public partial class AdvancedFind
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }

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

    protected override async Task OnParametersSetAsync()
    {
        ResetState();
        await LoadDataAsync();
    }

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

    private async Task LoadDataAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        StateHasChanged();

        try
        {
            var response = (await DataService.SearchClientsAsync(BuildRequest())).ToList();

            if (response.Count > 0)
            {
                Results.AddRange(response);
            }

            _hasMore = response.Count >= PageSize;
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

    private static string FormatIp(int? ip) =>
        ip.HasValue ? SharedLibraryCore.Utilities.ConvertIPtoString(ip.Value) : "-";

    private static string MakeAbbreviation(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var words = text.Split(' ');
        return words.Length == 1
            ? text
            : string.Concat(words.Where(w => w.Length > 0).Select(w => w[0]));
    }
}
