using Humanizer;
using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.UI.Display;

public partial class ActivitySparklineGraph : ComponentBase
{
    [Inject] public required AppState AppState { get; set; }

    [Parameter] public required IReadOnlyList<double> Data { get; set; }
    [Parameter] public TimeSpan TotalPlaytime { get; set; }

    private static string FormatPlaytime(TimeSpan duration) => duration.Humanize(maxUnit: TimeUnit.Hour);
}
