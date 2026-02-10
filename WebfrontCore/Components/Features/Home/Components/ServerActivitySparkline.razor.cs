using Humanizer;
using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Home.Components;

public partial class ServerActivitySparkline : ComponentBase
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required ILogger<ServerActivitySparkline> Logger { get; set; }

    [Parameter] public Data.Models.Reference.Game? Game { get; set; }

    private double[] _activityData = new double[30];
    private double _maxValue = 1;
    private TimeSpan _totalPlaytime;
    private Data.Models.Reference.Game? _previousGame;
    private bool _initialized;

    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized || _previousGame != Game)
        {
            _initialized = true;
            _previousGame = Game;
            await LoadActivityDataAsync();
        }
    }

    private async Task LoadActivityDataAsync()
    {
        try
        {
            var result = await DataService.GetServerActivitySparklineAsync(Game, null, CancellationToken.None);

            _activityData = result.DailyPlayTimeMinutes;
            _maxValue = _activityData.Length > 0 && _activityData.Max() > 0 ? _activityData.Max() : 1;
            _totalPlaytime = TimeSpan.FromMinutes(result.TotalPlaytimeMinutes);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading server activity sparkline data");
            _activityData = new double[30];
            _totalPlaytime = TimeSpan.Zero;
        }
    }

    private static string FormatPlaytime(TimeSpan duration)
    {
        return duration.Humanize();
    }
}
