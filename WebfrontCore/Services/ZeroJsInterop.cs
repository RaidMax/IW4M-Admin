using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace WebfrontCore.Services
{
    public class ZeroJsInterop : IZeroJsInterop
    {
        private readonly IJSRuntime _jsRuntime;

        public ZeroJsInterop(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task ToggleModal(string modalId)
        {
            await _jsRuntime.InvokeVoidAsync("halfmoon.toggleModal", modalId);
        }

        public async Task ToggleSidebar()
        {
            await _jsRuntime.InvokeVoidAsync("halfmoon.toggleSidebar");
        }

        public async Task ToggleDarkMode()
        {
            await _jsRuntime.InvokeVoidAsync("halfmoon.toggleDarkMode");
        }

        public async Task InitStickyAlert(string title, string content, string type, int duration = 5000)
        {
            await _jsRuntime.InvokeVoidAsync("halfmoon.initStickyAlert", new
            {
                title = title,
                content = content,
                alertType = type,
                timeShown = duration
            });
        }

        public async Task SetupInfiniteScroll<T>(ElementReference element, DotNetObjectReference<T> objRef) where T : class
        {
            await _jsRuntime.InvokeVoidAsync("blazorInfiniteScroll.setup", element, objRef);
        }

        public async Task RemoveInfiniteScroll(ElementReference element)
        {
            await _jsRuntime.InvokeVoidAsync("blazorInfiniteScroll.remove", element);
        }

        public async Task InitAdvancedStats(object historyData, object hitLocationData, float maxPercentage)
        {
            await _jsRuntime.InvokeVoidAsync("initAdvancedStats", historyData, hitLocationData, maxPercentage);
        }

        public async Task InitTopPlayersCharts()
        {
            // Initialize all rating history charts on the page
            await _jsRuntime.InvokeVoidAsync("eval", @"
                $('.client-rating-graph').each(function (i, element) {
                    var canvasId = $(element).children('canvas').attr('id');
                    if (canvasId) {
                        getStatsChart(canvasId);
                    }
                });
            ");
        }

        public async Task InitializeHalfmoon()
        {
            await _jsRuntime.InvokeVoidAsync("initHalfmoon");
        }
    }
}

