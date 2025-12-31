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
            const int pageSize = 200;
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var allConnections = new List<ConnectionHistoryResponse>();
            int offset = 0;
            bool hasMoreData = true;

            // Keep fetching until we have all data within 30 days or run out of results
            while (hasMoreData)
            {
                var meta = await DataService.GetClientMetaAsync(new Controllers.API.Models.ClientMetaRequest
                {
                    ClientId = ClientId,
                    Count = pageSize,
                    Offset = offset,
                    MetaType = MetaType.ConnectionHistory
                });

                var pageResults = meta.OfType<ConnectionHistoryResponse>()
                    .Where(c => c.ConnectionType == Data.Models.Reference.ConnectionType.Connect)
                    .ToList();

                if (pageResults.Count == 0)
                {
                    // No more results
                    hasMoreData = false;
                }
                else
                {
                    // Add connections within our date range
                    var validConnections = pageResults.Where(c => c.When >= thirtyDaysAgo).ToList();
                    allConnections.AddRange(validConnections);

                    // Check if we should continue:
                    // 1. Got a full page (might be more data)
                    // 2. Oldest item in this page is still within 30 days (need to check further back)
                    var oldestInPage = pageResults.Min(c => c.When);
                    hasMoreData = pageResults.Count == pageSize && oldestInPage >= thirtyDaysAgo;
                    offset += pageSize;
                }
            }

            // Group by day and count
            var grouped = allConnections
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
            _totalConnections = allConnections.Count;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading activity data: {ex.Message}");
            _activityData = new double[30];
            _totalConnections = 0;
        }
    }
}
