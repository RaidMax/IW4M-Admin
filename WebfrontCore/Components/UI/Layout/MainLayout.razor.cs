using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Auth;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.UI.Layout;

public partial class MainLayout
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required IHttpContextAccessor HttpContextAccessor { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }

    /// <summary>
    /// Bundle id of the currently-routed page when it comes from a plugin bundle (cascaded from
    /// Routes.razor), else null. The page body is wrapped in a <c>data-iw4m-plugin</c> marker so the
    /// plugin's scoped CSS targets it (see SharedLibraryCore PluginCssScoper). Host pages get no marker.
    /// </summary>
    [CascadingParameter(Name = "PluginScopeId")] private string? PluginScopeId { get; set; }

    private bool _isInitialized = false;
    private bool IsEnrollmentPage => NavManager.Uri.Contains("action=enroll2fa", StringComparison.OrdinalIgnoreCase);

    protected override void OnInitialized()
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
                    var hasTwoFactor = user.HasClaim(c => c.Type == WebfrontClaimTypes.HasTwoFactor && c.Value == "true");
                    var pendingTwoFactor = user.HasClaim(c => c.Type == WebfrontClaimTypes.PendingTwoFactorEnrollment);

                    if (!pendingTwoFactor && !hasTwoFactor &&
                        AppState.WebfrontConfig.RequireTwoFactorForPrivilegedClients &&
                        level >= EFClient.Permission.Moderator)
                    {
                        pendingTwoFactor = true;
                    }

                    AppState.InitializeUser(new ClientInfo
                    {
                        ClientId = clientId,
                        Name = nameClaim,
                        Level = level,
                        Game = game,
                        PendingTwoFactorEnrollment = pendingTwoFactor,
                        HasTwoFactor = hasTwoFactor
                    });
                }
            }
        }
        catch
        {
            // Handle errors gracefully
        }

        _isInitialized = true;
        AppState.OnChange += StateHasChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Theme is now handled entirely by JS in blazor_lib.js
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        AppState.OnChange -= StateHasChanged;
    }
}
