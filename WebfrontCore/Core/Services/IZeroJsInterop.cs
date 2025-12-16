using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace WebfrontCore.Core.Services;

public interface IZeroJsInterop
{
    Task ToggleModal(string modalId);
    Task ToggleSidebar();
    Task ToggleDarkMode();
    Task InitStickyAlert(string title, string content, string type, int duration = 5000);
    Task SetupInfiniteScroll<T>(ElementReference element, DotNetObjectReference<T> objRef) where T : class;
    Task RemoveInfiniteScroll(ElementReference element);
    Task InitAdvancedStats(object historyData, object hitLocationData, float maxPercentage);
    Task InitTopPlayersCharts();
    Task InitializeHalfmoon();
}
