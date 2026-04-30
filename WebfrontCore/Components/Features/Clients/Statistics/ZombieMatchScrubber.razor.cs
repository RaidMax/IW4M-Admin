using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class ZombieMatchScrubber : IAsyncDisposable
{
    [Parameter, EditorRequired] public ZombieScrubberPayload Payload { get; set; } = default!;

    /// <summary>
    /// When set, focuses that lane on initial render (dims others). Drives the
    /// share-link `?player={clientId}` deep-link case.
    /// </summary>
    [Parameter] public int? FocusedClientId { get; set; }

    private readonly string _elementId = $"zombie-scrubber-{Guid.NewGuid():N}";
    private DotNetObjectReference<ZombieMatchScrubber>? _dotnetRef;
    private bool _initialized;

    private ZombieScrubberPayload? _payload;
    private int? _lastFocusedClientId;
    private double _zoomLevel = 1;
    private string _filter = "all";
    // Mirrors JS-side default: 'qualified' when any lane qualifies, else 'all'. Re-evaluated
    // on payload change so a per-client view (single qualified lane) opens correctly.
    private string _laneMode = "qualified";
    private double _scrubSeconds;
    private string _scrubTimeLabel = string.Empty;
    private int _scrubRoundLabel;
    private List<WindowEvent> _windowEvents = [];

    private static readonly (string key, string locKey, string color)[] _filters =
    [
        ("all",       "WEBFRONT_ZOMBIE_TIMELINE_FILTER_ALL",     "text-foreground"),
        ("critical",  "WEBFRONT_ZOMBIE_TIMELINE_FILTER_DANGER",  "text-error"),
        ("powerups",  "WEBFRONT_ZOMBIE_TIMELINE_FILTER_DROPS",   "text-yellow-400"),
        ("economy",   "WEBFRONT_ZOMBIE_TIMELINE_FILTER_ECONOMY", "text-primary"),
    ];

    private int _minHeight => Payload is null ? 120 : Math.Max(120, 60 + Payload.Lanes.Count * 44);

    protected override void OnParametersSet()
    {
        // Payload identity change → match swap → full reinit (data shape may differ).
        if (!ReferenceEquals(_payload, Payload))
        {
            _payload = Payload;
            _laneMode = _payload.Lanes.Any(l => l.IsQualified) ? "qualified" : "all";
            _lastFocusedClientId = FocusedClientId;
            if (_initialized)
            {
                _ = ReinitializeAsync();
            }
            return;
        }

        // Same payload, focused player changed → soft update (preserves zoom/scroll).
        if (_initialized && _lastFocusedClientId != FocusedClientId)
        {
            _lastFocusedClientId = FocusedClientId;
            _ = JS.InvokeVoidAsync("zombieScrubber.focusClient", _elementId, FocusedClientId).AsTask();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _payload is not null)
        {
            _dotnetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("zombieScrubber.init", _elementId, _payload, _dotnetRef, FocusedClientId);
            _initialized = true;
        }
    }

    private async Task ReinitializeAsync()
    {
        if (_dotnetRef is null) return;
        await JS.InvokeVoidAsync("zombieScrubber.dispose", _elementId);
        await JS.InvokeVoidAsync("zombieScrubber.init", _elementId, _payload, _dotnetRef, FocusedClientId);
        UpdateWindowEvents();
        StateHasChanged();
    }

    private async Task SelectFilter(string key)
    {
        _filter = key;
        if (_initialized)
        {
            await JS.InvokeVoidAsync("zombieScrubber.setFilter", _elementId, key);
        }
    }

    private async Task ToggleLaneMode()
    {
        _laneMode = _laneMode == "qualified" ? "all" : "qualified";
        if (_initialized)
        {
            await JS.InvokeVoidAsync("zombieScrubber.setLaneMode", _elementId, _laneMode);
        }
    }

    private async Task MultiplyZoom(double factor)
    {
        _zoomLevel = Math.Clamp(_zoomLevel * factor, 1, 20);
        if (_initialized)
        {
            await JS.InvokeVoidAsync("zombieScrubber.setZoom", _elementId, _zoomLevel);
        }
    }

    private static string FormatZoom(double level) =>
        level >= 10 ? $"{level:F0}" : $"{level:F1}";

    /// <summary>
    /// Called from JS after wheel-zoom applies, so the toolbar +/- display reflects
    /// the actual JS-side zoom level. Button path also fires this (idempotent).
    /// </summary>
    [JSInvokable]
    public void OnZoomChanged(double level)
    {
        if (Math.Abs(_zoomLevel - level) < 0.001) return;
        _zoomLevel = level;
        StateHasChanged();
    }

    /// <summary>
    /// Called from JS when scrubber cursor moves. Debounced JS-side (~50ms).
    /// </summary>
    [JSInvokable]
    public void OnScrubChanged(double seconds)
    {
        _scrubSeconds = seconds;
        UpdateWindowEvents();
        StateHasChanged();
    }

    /// <summary>
    /// Called from JS when an event dot is clicked. Pins the side-panel window to that
    /// time across all lanes.
    /// </summary>
    [JSInvokable]
    public void OnEventClicked(int clientId, double seconds)
    {
        _scrubSeconds = seconds;
        UpdateWindowEvents();
        StateHasChanged();
    }

    private void UpdateWindowEvents()
    {
        if (_payload is null) { _windowEvents = []; return; }

        const double windowSeconds = 5;
        var lo = _scrubSeconds - windowSeconds;
        var hi = _scrubSeconds + windowSeconds;

        _windowEvents = _payload.Lanes
            .SelectMany(l => l.Events.Select(e => new WindowEvent
            {
                ClientId = l.ClientId,
                PlayerName = l.Name,
                Time = e.Time,
                Label = e.Label,
                Seconds = e.Seconds
            }))
            .Where(w => w.Seconds >= lo && w.Seconds <= hi)
            .OrderBy(w => w.Seconds)
            .Take(20)
            .ToList();

        _scrubTimeLabel = TimeSpan.FromSeconds(Math.Max(0, _scrubSeconds - _payload.MinSeconds))
            .ToString(@"mm\:ss");

        var bandAtScrub = _payload.RoundBands
            .FirstOrDefault(b => _scrubSeconds >= b.StartSeconds && _scrubSeconds <= b.EndSeconds);
        _scrubRoundLabel = bandAtScrub?.RoundNumber ?? 0;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_initialized)
            {
                await JS.InvokeVoidAsync("zombieScrubber.dispose", _elementId);
            }
        }
        catch (JSDisconnectedException) { /* circuit gone */ }
        catch (Microsoft.JSInterop.JSException) { /* page navigated */ }

        _dotnetRef?.Dispose();
    }

    private sealed class WindowEvent
    {
        public int ClientId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Seconds { get; set; }
    }
}
