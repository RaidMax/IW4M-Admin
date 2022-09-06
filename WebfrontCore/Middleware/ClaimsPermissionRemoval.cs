using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Data.Models.Client;
using Microsoft.AspNetCore.Authentication.Cookies;
using SharedLibraryCore.Commands;
using static SharedLibraryCore.GameEvent;

namespace WebfrontCore.Middleware
{
    /// <summary>
    /// Facilitates the removal of identity claims when client is demoted
    /// </summary>
    internal class ClaimsPermissionRemoval
    {
        private readonly IManager _manager;
        private static readonly ConcurrentDictionary<int, (ClaimsState, DateTimeOffset?)> PrivilegedClientIds = new();
        private readonly RequestDelegate _nextRequest;

        private enum ClaimsState
        {
            Current,
            Tainted
        }

        public ClaimsPermissionRemoval(RequestDelegate nextRequest, IManager manager)
        {
            _manager = manager;
            _manager.OnGameEventExecuted += OnGameEvent;
            _nextRequest = nextRequest;
        }

        public async Task Invoke(HttpContext context)
        {
            await Initialize();

            // sid stores the clientId
            var claimsId = context.User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Sid)?.Value;

            if (!string.IsNullOrEmpty(claimsId))
            {
                var clientId = int.Parse(claimsId);
                bool isTainted;
                bool hasPrivilege;

                lock (PrivilegedClientIds)
                {
                    hasPrivilege = PrivilegedClientIds.ContainsKey(clientId);
                    isTainted = hasPrivilege && PrivilegedClientIds[clientId].Item1 == ClaimsState.Tainted;
                }

                if (!hasPrivilege || isTainted)
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }

            await _nextRequest.Invoke(context);
        }

        private void OnGameEvent(object sender, GameEvent gameEvent)
        {
            if (gameEvent.Extra?.GetType() == typeof(SetPasswordCommand))
            {
                lock (PrivilegedClientIds)
                {
                    PrivilegedClientIds[gameEvent.Origin.ClientId] = (ClaimsState.Tainted, DateTimeOffset.UtcNow);
                }

                return;
            }

            if (gameEvent.Type != EventType.ChangePermission || gameEvent.Extra is not EFClient.Permission perm)
            {
                return;
            }

            lock (PrivilegedClientIds)
            {
                switch (perm)
                {
                    // we want to remove the claims when the client is demoted
                    case < EFClient.Permission.Trusted when PrivilegedClientIds.ContainsKey(gameEvent.Target.ClientId):
                    {
                        PrivilegedClientIds.Remove(gameEvent.Target.ClientId, out _);
                        break;
                    }
                    // and add if promoted
                    case > EFClient.Permission.Trusted:
                    {
                        if (!PrivilegedClientIds.ContainsKey(gameEvent.Target.ClientId))
                        {
                            PrivilegedClientIds.TryAdd(gameEvent.Target.ClientId, (ClaimsState.Current, null));
                        }
                        else
                        {
                            // they've been intra-moted, so we need to taint their claims
                            PrivilegedClientIds[gameEvent.Target.ClientId] =
                                (ClaimsState.Tainted, DateTimeOffset.UtcNow);
                        }

                        break;
                    }
                }
            }
        }

        private async Task Initialize()
        {
            // we want to load the initial list of privileged clients
            bool hasAny;
            lock (PrivilegedClientIds)
            {
                hasAny = PrivilegedClientIds.Any();
            }

            if (!hasAny)
            {
                var ids = (await _manager.GetClientService().GetPrivilegedClients())
                    .Select(client => client.ClientId);

                lock (PrivilegedClientIds)
                {
                    foreach (var id in ids)
                    {
                        PrivilegedClientIds.TryAdd(id, (ClaimsState.Current, null));
                    }
                }
            }
        }

        public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            if (context.Principal is null)
            {
                return;
            }

            var claimsId = context.Principal.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Sid)?.Value;

            if (string.IsNullOrEmpty(claimsId))
            {
                return;
            }

            var clientId = int.Parse(claimsId);

            bool shouldSignOut;

            lock (PrivilegedClientIds)
            {
                // we want to log them out if they aren't in the privileged clients list
                // or the token is tainted or the taint event occured after the token was generated
                shouldSignOut = PrivilegedClientIds.ContainsKey(clientId) &&
                                (PrivilegedClientIds[clientId].Item1 == ClaimsState.Tainted ||
                                 PrivilegedClientIds[clientId].Item2 is not null &&
                                 PrivilegedClientIds[clientId].Item2.Value - context.Properties.IssuedUtc >
                                 TimeSpan.FromSeconds(30));
            }

            if (shouldSignOut)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }

        public static Task OnSignedIn(CookieSignedInContext context)
        {
            if (context.Principal is null)
            {
                return Task.CompletedTask;
            }

            var claimsId = context.Principal.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Sid)?.Value;

            if (string.IsNullOrEmpty(claimsId))
            {
                return Task.CompletedTask;
            }

            var clientId = int.Parse(claimsId);

            lock (PrivilegedClientIds)
            {
                if (PrivilegedClientIds.ContainsKey(clientId))
                {
                    PrivilegedClientIds[clientId] = PrivilegedClientIds[clientId].Item1 == ClaimsState.Tainted
                        ? (ClaimsState.Current, DateTimeOffset.UtcNow)
                        : (ClaimsState.Current, null);
                }
            }

            return Task.CompletedTask;
        }
    }
}
