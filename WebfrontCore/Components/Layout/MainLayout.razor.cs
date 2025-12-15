using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Layout;

public partial class MainLayout
{
    [Inject] public required IZeroJsInterop JS { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required IHttpContextAccessor HttpContextAccessor { get; set; }
    [Inject] public required ApplicationConfiguration AppConfig { get; set; }
    private Shared.ActionModal _actionModal;
    private bool _isInitialized = false;
    private bool _halfmoonInitialized = false;
    private NavigationData _navData;
    private PeriodicTimer _badgeRefreshTimer;
    private CancellationTokenSource _cts;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var httpContext = HttpContextAccessor.HttpContext;

            // Read authenticated user directly from HttpContext.User (cookie-based auth)
            var user = httpContext?.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var clientIdClaim = user.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Sid)?.Value;
                var nameClaim = user.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var roleClaim = user.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
                var gameClaim = user.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.PrimaryGroupSid)?.Value;

                if (int.TryParse(clientIdClaim, out var clientId) &&
                    !string.IsNullOrEmpty(nameClaim) &&
                    !string.IsNullOrEmpty(roleClaim))
                {
                    var level = Enum.TryParse<EFClient.Permission>(roleClaim, out var parsedLevel)
                        ? parsedLevel
                        : EFClient.Permission.User;
                    var game = Enum.TryParse<Data.Models.Reference.Game>(gameClaim, out var parsedGame)
                        ? parsedGame
                        : Data.Models.Reference.Game.IW4;

                    AppState.SetUser(new ClientInfo
                    {
                        ClientId = clientId,
                        Name = nameClaim,
                        Level = level,
                        Game = game
                    });
                }
            }
        }
        catch
        {
            // Handle errors gracefully
        }

        try
        {
            // ... existing initialization logic ...
            _navData = await Api.GetNavigationDataAsync();
        }
        catch
        {
        }

        _isInitialized = true;

        // Start periodic badge refresh
        _cts = new CancellationTokenSource();
        _badgeRefreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = RefreshBadgesAsync();
        AppState.OnChange += StateHasChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isInitialized && !_halfmoonInitialized)
        {
            _halfmoonInitialized = true;
            await JS.InitializeHalfmoon();
            StateHasChanged();
        }
    }

    private async Task RefreshBadgesAsync()
    {
        try
        {
            while (await _badgeRefreshTimer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    _navData = await Api.GetNavigationDataAsync();
                    await InvokeAsync(StateHasChanged);
                }
                catch
                {
                    // Ignore refresh errors
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when component is disposed
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _badgeRefreshTimer?.Dispose();
        AppState.OnChange -= StateHasChanged;
    }

    private async Task ToggleDarkMode()
    {
        await JS.ToggleDarkMode();
    }
}
