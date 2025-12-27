using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Components;

public partial class ClientActivitySparkline
{
    [Inject] public required IWebfrontDataService DataService { get; set; }

    [Parameter] public int ClientId { get; set; }

    private double[] _activityData = new double[30];
    private double _maxValue = 1;
    private int _totalConnections;
    private int _previousClientId;

    protected override async Task OnParametersSetAsync()
    {
        if (_previousClientId != ClientId)
        {
            _previousClientId = ClientId;
            await LoadActivityDataAsync();
        }
    }

    private async Task LoadActivityDataAsync()
    {
        try
        {
            // Fetch connection history for last 30 days
            var meta = await DataService.GetClientMetaAsync(new Controllers.API.Models.ClientMetaRequest
            {
                ClientId = ClientId,
                Count = 200,
                MetaType = MetaType.ConnectionHistory
            });

            var connections = meta.OfType<ConnectionHistoryResponse>()
                .Where(c => c.ConnectionType == Data.Models.Reference.ConnectionType.Connect)
                .Where(c => c.When >= DateTime.UtcNow.AddDays(-30))
                .ToList();

            // Group by day and count
            var grouped = connections
                .GroupBy(c => c.When.Date)
                .ToDictionary(g => g.Key, g => (double)g.Count());

            // Fill the 30-day array
            _activityData = new double[30];
            for (int i = 0; i < 30; i++)
            {
                var date = DateTime.UtcNow.Date.AddDays(-29 + i);
                _activityData[i] = grouped.TryGetValue(date, out var count) ? count : 0;
            }

            _maxValue = _activityData.Max() is var max && max > 0 ? max : 1;
            _totalConnections = connections.Count;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading activity data: {ex.Message}");
            _activityData = new double[30];
            _totalConnections = 0;
        }
    }

    private string GetTooltip()
    {
        return $"Activity over last 30 days: {_totalConnections} connections";
    }
}
