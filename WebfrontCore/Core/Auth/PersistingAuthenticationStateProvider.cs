using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using SharedLibraryCore.Dtos;
using System.Security.Claims;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Core.Auth;

public class PersistingAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
{
    private readonly PersistentComponentState _persistentComponentState;
    private readonly PersistingComponentStateSubscription _subscription;
    private readonly AppState _appState;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Task<AuthenticationState>? _authenticationStateTask;

    public PersistingAuthenticationStateProvider(PersistentComponentState persistentComponentState, AppState appState, IHttpContextAccessor httpContextAccessor)
    {
        _persistentComponentState = persistentComponentState;
        _appState = appState;
        _httpContextAccessor = httpContextAccessor;
        
        // Register callback to persist the state during SSR
        _subscription = _persistentComponentState.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveServer);
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return await (_authenticationStateTask ??= CreateAuthenticationStateAsync());
    }

    private async Task<AuthenticationState> CreateAuthenticationStateAsync()
    {
        await Task.Yield();

        // 1. Try to restore from persisted state (Interactive mode optimization)
        if (_persistentComponentState.TryTakeFromJson<ClientInfo>("UserInfo", out var userInfo) && userInfo != null)
        {
            // Restore global AppState from the persisted info
            _appState.SetUser(userInfo);
            return CreateStateFromUser(userInfo);
        }

        // 2. If no persisted state, check HttpContext (SSR mode)
        var httpContextUser = _httpContextAccessor.HttpContext?.User;
        if (httpContextUser?.Identity?.IsAuthenticated == true)
        {
            // Hydrate AppState from the current HttpContext user so it can be persisted later
            var clientInfo = ParseClientInfo(httpContextUser);
            if (clientInfo != null)
            {
                _appState.SetUser(clientInfo);
                // Return the state directly from the HttpContext user (or reconstructed)
                return new AuthenticationState(httpContextUser);
            }
        }
        
        // 3. Fallback to base ServerAuthenticationStateProvider (Standard Circuit Auth)
        // This handles cases like iOS Long Polling where persisted state transfer fails but the circuit is authenticated
        var baseState = await base.GetAuthenticationStateAsync();
        if (baseState.User.Identity?.IsAuthenticated == true)
        {
             var clientInfo = ParseClientInfo(baseState.User);
             if (clientInfo != null)
             {
                 _appState.SetUser(clientInfo);
             }
             return baseState;
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    private Task OnPersistingAsync()
    {
        // This runs during SSR. 
        var user = _appState.User; 
        
        if (user != null)
        {
            _persistentComponentState.PersistAsJson("UserInfo", user);
        }

        return Task.CompletedTask;
    }

    private AuthenticationState CreateStateFromUser(ClientInfo userInfo)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userInfo.Name),
            new Claim(ClaimTypes.Role, userInfo.Level.ToString()),
            new Claim(ClaimTypes.Sid, userInfo.ClientId.ToString()),
            new Claim(ClaimTypes.PrimarySid, "0"), 
            new Claim(ClaimTypes.PrimaryGroupSid, userInfo.Game.ToString())
        };
        
        var identity = new ClaimsIdentity(claims, "PersistentState");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private ClientInfo? ParseClientInfo(ClaimsPrincipal? principal)
    {
        if (principal == null) return null;

        var clientIdStr = principal.FindFirst(ClaimTypes.Sid)?.Value;
        var name = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var levelStr = principal.FindFirst(ClaimTypes.Role)?.Value;
        var gameStr = principal.FindFirst(ClaimTypes.PrimaryGroupSid)?.Value;

        if (int.TryParse(clientIdStr, out int clientId) && 
            Enum.TryParse<Data.Models.Client.EFClient.Permission>(levelStr, out var level))
        {
             var game = Enum.TryParse<Data.Models.Reference.Game>(gameStr, out var g) ? g : Data.Models.Reference.Game.IW4;
             
             return new ClientInfo
             {
                 ClientId = clientId,
                 Name = name ?? "Unknown",
                 Level = level,
                 Game = game
             };
        }

        return null;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
