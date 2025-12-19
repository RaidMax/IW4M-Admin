using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.UI.Layout;

public partial class MainLayout
{
    [Inject] public required IZeroJsInterop JS { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required IHttpContextAccessor HttpContextAccessor { get; set; }
    [Inject] public required ApplicationConfiguration AppConfig { get; set; }
    private bool _isInitialized = false;
    private bool _halfmoonInitialized = false;
    private bool _sidebarOpen = false;

    private void ToggleSidebar()
    {
        _sidebarOpen = !_sidebarOpen;
    }

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
            // ... existing initialization logic ...
        }
        catch
        {
        }

        _isInitialized = true;
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



    public void Dispose()
    {
        AppState.OnChange -= StateHasChanged;
    }

    private async Task ToggleDarkMode()
    {
        await JS.ToggleDarkMode();
    }
}
