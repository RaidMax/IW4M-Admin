using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;
using WebCommon.Services;

namespace WebfrontCore.Components.Features.Servers.Components;

public partial class ServerCard : IAsyncDisposable
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required ILogger<ServerCard> Logger { get; set; }
    [Parameter, EditorRequired] public ServerInfo Model { get; set; } = default!;
    [Parameter] public EventCallback<string> OnChat { get; set; }

    private ElementReference _cardElement;
    private DotNetObjectReference<ServerCard>? _dotNetRef;
    private PeriodicTimer? _timer;
    private CancellationTokenSource _cts = new();
    // Tracked so DisposeAsync can await the loop to fully exit before
    // disposing _cts/_timer. Without this, the fire-and-forget loop would
    // re-read _cts.Token on its next iteration after Dispose ran and throw
    // ObjectDisposedException — observed as a flood of "Error in ServerCard
    // refresh timer" log lines on page teardown (every visible card races
    // simultaneously).
    private Task? _runnerTask;
    private bool _isVisible = true; // Default to visible for initial render
    private bool _showMobileDetails;

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

            // Start the refresh timer. Capture the task so DisposeAsync can
            // await it before tearing down _cts/_timer (see _runnerTask doc).
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            _runnerTask = RunTimerAsync();
        }
    }

    [JSInvokable]
    public void OnVisibilityChanged(bool isVisible)
    {
        _isVisible = isVisible;
    }

    private async Task RunTimerAsync()
    {
        // Capture the token once — re-reading _cts.Token on each iteration
        // would throw ObjectDisposedException if Dispose ran between ticks.
        // The token still observes cancellation because the source signals
        // it before being disposed.
        var token = _cts.Token;
        try
        {
            while (await _timer!.WaitForNextTickAsync(token))
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
        catch (ObjectDisposedException)
        {
            // Race between Dispose and an in-flight WaitForNextTickAsync —
            // PeriodicTimer.Dispose throws here. Safe to swallow at teardown.
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

    private async Task HandlePlayClick(MouseEventArgs e)
    {
        // Modifier-click copies the connect command to clipboard instead of opening
        // the protocol handler. The icon swap (play -> clipboard) is driven by a
        // body-level CSS class set on Ctrl/Meta keydown in blazor_lib.js, so the
        // affordance is visible before the user actually clicks.
        if (e.CtrlKey || e.MetaKey)
        {
            var connectCmd = $"connect {Model.ExternalIPAddress}:{Model.Port}";
            var ok = await JS.InvokeAsync<bool>("copyToClipboard", connectCmd);
            if (ok)
            {
                await ToastService.ShowSuccessAsync(connectCmd, AppState.Loc("WEBFRONT_HOME_JOIN_COPIED"));
            }
            else
            {
                await ToastService.ShowErrorAsync(AppState.Loc("WEBFRONT_HOME_JOIN_COPY_FAILED"));
            }
            return;
        }

        await JS.InvokeVoidAsync("openProtocolUrl", Model.ConnectProtocolUrl);
    }

    private void OpenScoreboard()
    {
        ActionService.OpenCustom(new ModalRequest(ScoreboardContent(Model.Id), Model.Name.StripColors())
        {
            ModalClass = "max-w-5xl max-h-[90vh]"
        });
    }

    private RenderFragment ScoreboardContent(string serverId) => builder =>
    {
        builder.OpenComponent(0, typeof(ScoreboardModalWrapper));
        builder.AddAttribute(1, nameof(ScoreboardModalWrapper.ServerId), serverId);
        builder.CloseComponent();
    };

    public async ValueTask DisposeAsync()
    {
        // Order matters: cancel → await the loop to fully exit → dispose
        // the source. Disposing _cts while the loop is mid-iteration races
        // its next _cts.Token read and throws ObjectDisposedException.
        await _cts.CancelAsync();
        if (_runnerTask is not null)
        {
            try { await _runnerTask; }
            catch { /* loop logs its own errors; teardown swallows. */ }
        }
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
