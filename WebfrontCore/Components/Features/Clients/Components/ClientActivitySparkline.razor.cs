using Humanizer;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Components;

public partial class ClientActivitySparkline
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required ILogger<ClientActivitySparkline> Logger { get; set; }

    [Parameter] public int ClientId { get; set; }

    // Stores playtime in minutes per day
    private double[] _activityData = new double[30];
    private double _maxValue = 1;
    private TimeSpan _totalPlaytime;
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
            var allConnectionEvents = new List<ConnectionHistoryResponse>();
            var offset = 0;
            var hasMoreData = true;

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

                // Get all connection events (both Connect and Disconnect)
                var pageResults = meta.OfType<ConnectionHistoryResponse>().ToList();

                if (pageResults.Count == 0)
                {
                    hasMoreData = false;
                }
                else
                {
                    // Add events within our date range
                    var validEvents = pageResults.Where(c => c.When >= thirtyDaysAgo).ToList();
                    allConnectionEvents.AddRange(validEvents);

                    // Check if we should continue
                    var oldestInPage = pageResults.Min(c => c.When);
                    hasMoreData = pageResults.Count == pageSize && oldestInPage >= thirtyDaysAgo;
                    offset += pageSize;
                }
            }

            // Calculate playtime per day by pairing Connect/Disconnect events PER SERVER
            var dailyPlaytime = CalculateDailyPlaytime(allConnectionEvents);

            // Fill the 30-day array (values in minutes)
            _activityData = new double[30];
            for (var i = 0; i < 30; i++)
            {
                var date = DateTime.UtcNow.Date.AddDays(-29 + i);
                _activityData[i] = dailyPlaytime.TryGetValue(date, out var minutes) ? minutes : 0;
            }

            _maxValue = _activityData.Max() is var max and > 0 ? max : 1;
            _totalPlaytime = TimeSpan.FromMinutes(_activityData.Sum());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading activity data");
            _activityData = new double[30];
            _totalPlaytime = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Calculates playtime per day by pairing Connect and Disconnect events.
    /// Sessions are tracked per-server since a user can be connected to multiple servers.
    /// </summary>
    private Dictionary<DateTime, double> CalculateDailyPlaytime(List<ConnectionHistoryResponse> events)
    {
        var dailyPlaytime = new Dictionary<DateTime, double>();

        // Group events by server and process each server's sessions independently
        var eventsByServer = events.GroupBy(e => e.ServerName ?? "Unknown");

        foreach (var serverEvents in eventsByServer)
        {
            // Sort events chronologically for this server
            var sortedEvents = serverEvents.OrderBy(e => e.When).ToList();

            // Track active session start time for this server
            DateTime? sessionStart = null;

            foreach (var evt in sortedEvents)
            {
                if (evt.ConnectionType == Data.Models.Reference.ConnectionType.Connect)
                {
                    // If there was already an active session on this server, close it first
                    // (handles edge case of missing disconnect)
                    if (sessionStart.HasValue)
                    {
                        AddPlaytimeToDay(dailyPlaytime, sessionStart.Value, evt.When);
                    }
                    sessionStart = evt.When;
                }
                else if (evt.ConnectionType == Data.Models.Reference.ConnectionType.Disconnect)
                {
                    if (sessionStart.HasValue)
                    {
                        AddPlaytimeToDay(dailyPlaytime, sessionStart.Value, evt.When);
                        sessionStart = null;
                    }
                    // Ignore disconnects without a preceding connect in our window
                }
            }

            // Don't count active sessions - only count completed sessions
            // (Active sessions would inflate the count unrealistically)
        }

        return dailyPlaytime;
    }

    /// <summary>
    /// Adds playtime to the appropriate day(s). Handles sessions spanning midnight.
    /// </summary>
    private static void AddPlaytimeToDay(Dictionary<DateTime, double> dailyPlaytime, DateTime start, DateTime end)
    {
        // Sanity check - ignore sessions longer than 24 hours (likely data issue)
        if ((end - start).TotalHours > 24)
        {
            return;
        }

        var current = start;

        while (current.Date < end.Date)
        {
            // Add time from current to end of day
            var endOfDay = current.Date.AddDays(1);
            var minutes = (endOfDay - current).TotalMinutes;
            AddMinutesToDate(dailyPlaytime, current.Date, minutes);
            current = endOfDay;
        }

        // Add remaining time on the final day
        var finalMinutes = (end - current).TotalMinutes;
        if (finalMinutes > 0)
        {
            AddMinutesToDate(dailyPlaytime, current.Date, finalMinutes);
        }
    }

    private static void AddMinutesToDate(Dictionary<DateTime, double> dict, DateTime date, double minutes)
    {
        if (dict.TryGetValue(date, out var existing))
        {
            dict[date] = existing + minutes;
        }
        else
        {
            dict[date] = minutes;
        }
    }

    /// <summary>
    /// Formats a TimeSpan for tooltip display using Humanizer.
    /// </summary>
    private static string FormatPlaytime(TimeSpan duration)
    {
        return duration.Humanize();
    }
}
