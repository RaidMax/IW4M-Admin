using Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Servers.Components;

public partial class ServerCard : IAsyncDisposable
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required ILogger<ServerCard> Logger { get; set; }
    [Parameter, EditorRequired] public ServerInfo Model { get; set; } = default!;
    [Parameter] public EventCallback<string> OnChat { get; set; }

    /// <summary>
    /// Games that ship a banner image under wwwroot/images/banners/{game}.jpg
    /// </summary>
    private static readonly HashSet<string> BannerGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "csgo", "d7d", "h1", "h2m", "iw3", "iw4", "iw5", "iw6", "iw7", "shg1", "t4", "t5", "t6", "t7"
    };

    private const string ActionButtonClass =
        "w-8 h-8 rounded-md flex items-center justify-center bg-background/70 border border-white/10 backdrop-blur text-subtle hover:text-foreground hover:bg-surface-hover transition-colors";

    private ElementReference _cardElement;
    private DotNetObjectReference<ServerCard>? _dotNetRef;
    private PeriodicTimer? _timer;
    private CancellationTokenSource _cts = new();
    private bool _isVisible = true; // Default to visible for initial render
    private bool _expanded;

    private bool HasBanner => BannerGames.Contains(Model.Game.ToString());

    private string BannerStyle => HasBanner
        ? $"background-image:url('/images/banners/{Model.Game.ToString().ToLowerInvariant()}.jpg')"
        : string.Empty;

    private string GameLabel => Model.Game switch
    {
        Reference.Game.D7D => "7DTD",
        _ => Model.Game.ToString()
    };

    private int PublicSlots => Math.Max(0, Model.MaxClients - Model.PrivateClientSlots);

    private int FillPercent => PublicSlots <= 0
        ? 0
        : Math.Clamp((int)Math.Round(100.0 * Model.ClientCount / PublicSlots), 0, 100);

    private string SlotNote
    {
        get
        {
            if (!Model.Online)
            {
                return AppState.LocOr("WEBFRONT_SCRIPT_SERVER_UNREACHABLE", "Unreachable");
            }

            if (PublicSlots > 0 && Model.ClientCount >= PublicSlots)
            {
                return AppState.LocOr("WEBFRONT_SERVER_FULL", "Full");
            }

            if (Model.PrivateClientSlots > 0)
            {
                return $"{Model.PrivateClientSlots} {AppState.LocOr("WEBFRONT_SERVER_RESERVED", "reserved")}";
            }

            return string.Empty;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Model?.ClientHistory?.ClientCounts != null)
        {
            var strings = new
            {
                players = AppState.Loc("WEBFRONT_SCRIPT_SERVER_PLAYERS"),
                unreachable = AppState.Loc("WEBFRONT_SCRIPT_SERVER_UNREACHABLE")
            };
            // Always update the chart with latest data
            await JS.InvokeVoidAsync("updateServerChart", $"server_history_canvas_{Model.Id}",
                Model.ClientHistory.ClientCounts, Model.MaxClients, strings);
        }

        if (firstRender)
        {
            // Set up visibility observer
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("visibilityObserver.observe", _cardElement, _dotNetRef);

            // Start the refresh timer
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            _ = RunTimerAsync();
        }
    }

    [JSInvokable]
    public void OnVisibilityChanged(bool isVisible)
    {
        _isVisible = isVisible;
    }

    private async Task RunTimerAsync()
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(_cts.Token))
            {
                // Only refresh when visible
                if (_isVisible)
                {
                    await Refresh();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposing
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in ServerCard refresh timer");
        }
    }

    private async Task Refresh()
    {
        try
        {
            var updated = await DataService.GetServer(Model.Id);
            if (updated != null)
            {
                Model = updated;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch
        {
            // Silently handle refresh errors
        }
    }

    private void ToggleExpanded()
    {
        _expanded = !_expanded;
    }

    private void OpenScoreboard()
    {
        ActionService.OpenCustom(ScoreboardContent(Model.Id), Model.Name.StripColors(), "max-w-5xl");
    }

    private RenderFragment ScoreboardContent(string serverId) => builder =>
    {
        builder.OpenComponent(0, typeof(ScoreboardModalWrapper));
        builder.AddAttribute(1, nameof(ScoreboardModalWrapper.ServerId), serverId);
        builder.CloseComponent();
    };

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        _timer?.Dispose();

        try
        {
            await JS.InvokeVoidAsync("visibilityObserver.unobserve", _cardElement);

            // Clean up cached chart instance to prevent overlay issues on game filter change
            if (Model?.Id != null)
            {
                await JS.InvokeVoidAsync("destroyServerChart", $"server_history_canvas_{Model.Id}");
            }
        }
        catch
        {
            // JS interop may fail during app shutdown
        }

        _dotNetRef?.Dispose();
    }
}
