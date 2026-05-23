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

    /// <summary>
    /// Externally controls whether the scrubber surfaces unqualified drop-in
    /// lanes alongside the qualifying roster. Driven by the consumer's
    /// SHOW_ALL toggle (e.g. the leaderboard card's WEBFRONT_ZOMBIE_LEADERBOARD_SHOW_ALL
    /// button) so a single user-facing control governs both the scoreboard's
    /// visible rows and the timeline's visible lanes — no two-toggle drift.
    /// Default false (qualified-only) matches the leaderboard's default and the
    /// "this is a record of the qualifying run" framing.
    /// </summary>
    [Parameter] public bool ShowAllPlayers { get; set; }

    private readonly string _elementId = $"zombie-scrubber-{Guid.NewGuid():N}";
    private DotNetObjectReference<ZombieMatchScrubber>? _dotnetRef;
    private bool _initialized;

    private ZombieScrubberPayload? _payload;
    private int? _lastFocusedClientId;
    private bool _lastShowAllPlayers;
    private double _zoomLevel = 1;
    private string _filter = "all";
    // Derived from the ShowAllPlayers parameter + share-link auto-promote (see
    // EnsureLaneModeShowsFocused). The JS side reads "qualified" / "all" strings;
    // we keep that wire format internally so existing JS doesn't need to change.
    private string _laneMode = "qualified";
    private double _scrubSeconds;
    // JS-computed zoom-aware hit-window; defaults to ±5s pre-first-callback.
    private double _scrubHalfWindow = 5;
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

    // Container height tracks the lanes JS will actually render — when the consumer
    // toggles SHOW_ALL off, the unqualified lanes are hidden client-side but the
    // container would otherwise keep their reserved vertical space. Shrinking with the
    // visible lane count keeps the timeline tight and avoids awkward dead space.
    // Constants mirror zombie-scrubber.js _trackHeight — keep in sync.
    private int _minHeight
    {
        get
        {
            if (Payload is null) return 100;
            var visibleLanes = _laneMode == "all"
                ? Payload.Lanes.Count
                : Payload.Lanes.Count(l => l.IsQualified);
            const int topPad = 24;
            const int laneRow = 44;
            var tickband = Payload.MatchLevelEvents.Count > 0 ? 32 : 0;
            return topPad + tickband + Math.Max(1, visibleLanes) * laneRow;
        }
    }

    protected override void OnParametersSet()
    {
        // Payload identity change → match swap → full reinit (data shape may differ).
        if (!ReferenceEquals(_payload, Payload))
        {
            _payload = Payload;
            ResolveLaneMode();
            _lastFocusedClientId = FocusedClientId;
            _lastShowAllPlayers = ShowAllPlayers;
            if (_initialized)
            {
                _ = ReinitializeAsync();
            }
            return;
        }

        // Same payload, but focused player or external ShowAllPlayers toggle
        // changed → soft update (preserves zoom/scroll position).
        var focusChanged = _lastFocusedClientId != FocusedClientId;
        var showAllChanged = _lastShowAllPlayers != ShowAllPlayers;
        if (_initialized && (focusChanged || showAllChanged))
        {
            _lastFocusedClientId = FocusedClientId;
            _lastShowAllPlayers = ShowAllPlayers;
            var previousLaneMode = _laneMode;
            ResolveLaneMode();
            if (_laneMode != previousLaneMode)
            {
                _ = JS.InvokeVoidAsync("zombieScrubber.setLaneMode", _elementId, _laneMode).AsTask();
            }
            if (focusChanged)
            {
                _ = JS.InvokeVoidAsync("zombieScrubber.focusClient", _elementId, FocusedClientId).AsTask();
            }
        }
    }

    /// <summary>
    /// Resolves <see cref="_laneMode"/> from the external <see cref="ShowAllPlayers"/>
    /// parameter, with one override: when the focused client maps to an unqualified
    /// lane, force "all" so the share-link `?player={clientId}` case never lands on
    /// a hidden lane. Without that override, deep-linking to a drop-in player would
    /// silently hide the lane the user explicitly asked to see.
    /// </summary>
    private void ResolveLaneMode()
    {
        _laneMode = ShowAllPlayers ? "all" : "qualified";

        if (_laneMode == "qualified" && FocusedClientId is not null && _payload is not null)
        {
            var focused = _payload.Lanes.FirstOrDefault(l => l.ClientId == FocusedClientId);
            if (focused is not null && !focused.IsQualified)
            {
                _laneMode = "all";
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _payload is not null)
        {
            _dotnetRef = DotNetObjectReference.Create(this);
            // Pass _laneMode explicitly — JS would otherwise default to 'qualified'
            // when any lane qualifies, ignoring our resolved state. That mismatch
            // bit the dedicated match page (ShowAllPlayers=true → Razor _laneMode
            // = "all" but JS picked "qualified" → drop-ins missing on first paint).
            await JS.InvokeVoidAsync("zombieScrubber.init", _elementId, _payload, _dotnetRef, FocusedClientId, _laneMode);
            _initialized = true;
        }
    }

    private async Task ReinitializeAsync()
    {
        if (_dotnetRef is null) return;
        await JS.InvokeVoidAsync("zombieScrubber.dispose", _elementId);
        await JS.InvokeVoidAsync("zombieScrubber.init", _elementId, _payload, _dotnetRef, FocusedClientId, _laneMode);
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
    /// <paramref name="halfWindowSeconds"/> is JS-computed so the panel's hit
    /// window scales with the current zoom — dot footprint visual width maps
    /// to hit-window width at any zoom level.
    /// </summary>
    [JSInvokable]
    public void OnScrubChanged(double seconds, double halfWindowSeconds)
    {
        _scrubSeconds = seconds;
        _scrubHalfWindow = halfWindowSeconds > 0 ? halfWindowSeconds : 5;
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

        var lo = _scrubSeconds - _scrubHalfWindow;
        var hi = _scrubSeconds + _scrubHalfWindow;

        _windowEvents = _payload.Lanes
            .SelectMany(l => l.Events
                // Round-completion markers are excluded from the side panel —
                // the R{n} round-band labels on the track convey the same info,
                // and "Round 3" rows for every lane within a 5s window of a
                // band boundary added noise without insight.
                .Where(e => !string.Equals(e.Category, "round", StringComparison.OrdinalIgnoreCase))
                .Select(e => new WindowEvent
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
