using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Security.Claims;
using Data.Models.Client;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Dtos;
using static SharedLibraryCore.GameEvent;

namespace WebfrontCore.Core.Middleware;

/// <summary>
/// Rejects webfront session cookies that no longer match the client they were issued to.
/// A cookie is accepted only while the client exists, is active, is at least Trusted, still has the
/// level written in the cookie's role claim, and the cookie was issued after the client's sessions were
/// last revoked (password change or level change). Every decision is made against the database, so it
/// holds across restarts; the in-memory state is only a short-lived cache of it.
/// A rejected request continues as anonymous: signing out alone only asks the browser to drop the cookie.
/// </summary>
internal class ClaimsPermissionRemoval
{
    /// <summary>how long a cached session state is trusted before it is read from the database again</summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// cache of per-client session state, keyed by client id. clearing it is equivalent to a restart.
    /// (name kept from when it held only privileged client ids)
    /// </summary>
    private static readonly ConcurrentDictionary<int, CachedSessionState> PrivilegedClientIds = new();

    /// <summary>
    /// revocations not yet known to be in the database: still being written, or whose write failed.
    /// a reload waits for them and, if the write failed, applies the revocation anyway (fail closed)
    /// </summary>
    private static readonly ConcurrentDictionary<int, PendingRevocation> PendingRevocations = new();

    private readonly IManager _manager;
    private readonly RequestDelegate _nextRequest;

    private sealed record CachedSessionState(WebfrontSessionState? State, DateTime LoadedAt);

    private sealed record PendingRevocation(Task Write, DateTime RevokedAt);

    public ClaimsPermissionRemoval(RequestDelegate nextRequest, IManager manager)
    {
        _manager = manager;
        _manager.OnGameEventExecuted += OnGameEvent;
        _nextRequest = nextRequest;
    }

    public async Task Invoke(HttpContext context)
    {
        var clientId = GetClientId(context.User);

        if (clientId is not null && context.User.Identity?.IsAuthenticated == true)
        {
            var issuedUtc = context.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult?.Properties
                ?.IssuedUtc;
            var state = await GetStateAsync(clientId.Value);

            if (ShouldReject(context.User, issuedUtc, state))
            {
                // drop the identity for this request too; the sign out only tells the browser to discard the cookie
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }

        await _nextRequest.Invoke(context);
    }

    private async Task<WebfrontSessionState?> GetStateAsync(int clientId)
    {
        if (PrivilegedClientIds.TryGetValue(clientId, out var cached) &&
            DateTime.UtcNow - cached.LoadedAt < CacheLifetime)
        {
            return cached.State;
        }

        DateTime? unpersistedRevocation = null;
        if (PendingRevocations.TryGetValue(clientId, out var pending))
        {
            try
            {
                await pending.Write;
            }
            catch
            {
                // logged by the writer. the revocation is not in the database: keep applying it from memory
                // and try to write it again
                unpersistedRevocation = pending.RevokedAt;
                StartPersisting(clientId, pending.RevokedAt);
            }
        }

        var state = await _manager.GetClientService().GetWebfrontSessionState(clientId);

        if (state is not null && unpersistedRevocation is { } revokedAt &&
            (state.SessionsRevokedAt is null || state.SessionsRevokedAt < revokedAt))
        {
            state = state with { SessionsRevokedAt = revokedAt };
        }

        PrivilegedClientIds[clientId] = new CachedSessionState(state, DateTime.UtcNow);
        return state;
    }

    private static bool ShouldReject(ClaimsPrincipal principal, DateTimeOffset? issuedUtc, WebfrontSessionState? state)
    {
        if (state is null || !state.Active || state.Level < EFClient.Permission.Trusted)
        {
            return true;
        }

        // the cookie's role must still be the client's level; a demotion or promotion ends the session
        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        if (!string.Equals(role, state.Level.ToString(), StringComparison.Ordinal))
        {
            return true;
        }

        // cookie issue times have one-second resolution, so a cookie issued in the same second as the
        // revocation is treated as issued before it
        return state.SessionsRevokedAt is { } revokedAt &&
               (issuedUtc is null || issuedUtc.Value.UtcDateTime <= revokedAt);
    }

    private void OnGameEvent(object? sender, GameEvent gameEvent)
    {
        if (gameEvent.Failed)
        {
            return;
        }

        if (gameEvent.Extra?.GetType() == typeof(SetPasswordCommand) && gameEvent.Origin is not null)
        {
            Revoke(gameEvent.Origin.ClientId, null);
            return;
        }

        if (gameEvent.Type != EventType.ChangePermission || gameEvent.Extra is not EFClient.Permission perm ||
            gameEvent.Target is null)
        {
            return;
        }

        if (perm >= EFClient.Permission.Trusted)
        {
            Revoke(gameEvent.Target.ClientId, perm);
            return;
        }

        // below Trusted no session is accepted at all, so there is nothing to persist (this also covers
        // ordinary players being banned or flagged); just stop trusting a cached privileged level
        if (PrivilegedClientIds.TryGetValue(gameEvent.Target.ClientId, out var cached) && cached.State is not null)
        {
            PrivilegedClientIds[gameEvent.Target.ClientId] =
                new CachedSessionState(cached.State with { Level = perm }, DateTime.UtcNow);
        }
    }

    /// <summary>
    /// invalidates every existing session of the client, immediately in memory and durably in the database
    /// </summary>
    private void Revoke(int clientId, EFClient.Permission? newLevel)
    {
        var revokedAt = DateTime.UtcNow;

        // registered before the cache changes, so a request that misses the cache waits for the write
        StartPersisting(clientId, revokedAt);

        if (newLevel is null)
        {
            // password change: the next request reads the (revoked) state from the database
            PrivilegedClientIds.TryRemove(clientId, out _);
            return;
        }

        // level change: apply it at once, in case the database write of the level lands after this event
        PrivilegedClientIds.TryGetValue(clientId, out var existing);
        PrivilegedClientIds[clientId] = new CachedSessionState(
            new WebfrontSessionState(newLevel.Value, existing?.State?.Active ?? true, revokedAt), revokedAt);
    }

    private void StartPersisting(int clientId, DateTime revokedAt)
    {
        var pending = new PendingRevocation(PersistRevocationAsync(clientId, revokedAt), revokedAt);
        PendingRevocations[clientId] = pending;

        _ = pending.Write.ContinueWith(write =>
        {
            // a failed write stays pending so the revocation keeps being applied; reading the exception
            // marks it observed
            if (write.IsCompletedSuccessfully)
            {
                PendingRevocations.TryRemove(new KeyValuePair<int, PendingRevocation>(clientId, pending));
            }
            else
            {
                _ = write.Exception;
            }
        }, TaskScheduler.Default);
    }

    private async Task PersistRevocationAsync(int clientId, DateTime revokedAt)
    {
        try
        {
            await _manager.GetClientService().RevokeWebfrontSessions(clientId, revokedAt);
        }
        catch (Exception ex)
        {
            Utilities.DefaultLogger?.LogError(ex,
                "Could not persist the revocation of webfront sessions for client {ClientId}", clientId);
            throw;
        }
    }

    private static int? GetClientId(ClaimsPrincipal? principal)
    {
        var claimsId = principal?.FindFirst(ClaimTypes.Sid)?.Value;
        return int.TryParse(claimsId, out var clientId) ? clientId : null;
    }

    /// <summary>
    /// cookie validation hook: rejects a principal the cached state already shows to be invalid.
    /// the authoritative, database-backed check runs in <see cref="Invoke" />
    /// </summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var clientId = GetClientId(context.Principal);
        if (clientId is null || !PrivilegedClientIds.TryGetValue(clientId.Value, out var cached) ||
            DateTime.UtcNow - cached.LoadedAt >= CacheLifetime)
        {
            return;
        }

        if (ShouldReject(context.Principal!, context.Properties.IssuedUtc, cached.State))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public static Task OnSignedIn(CookieSignedInContext context)
    {
        // a new cookie was issued; read the client's state afresh on its next request
        var clientId = GetClientId(context.Principal);
        if (clientId is not null)
        {
            PrivilegedClientIds.TryRemove(clientId.Value, out _);
        }

        return Task.CompletedTask;
    }
}
