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
using static SharedLibraryCore.GameEvent;

namespace WebfrontCore.Middleware
{
    /// <summary>
    /// Facilitates the removal of identity claims when client is demoted
    /// </summary>
    internal class ClaimsPermissionRemoval
    {
        private readonly IManager _manager;
        private readonly List<int> _privilegedClientIds;
        private readonly RequestDelegate _nextRequest;

        public ClaimsPermissionRemoval(RequestDelegate nextRequest, IManager manager)
        {
            _manager = manager;
            _manager.OnGameEventExecuted += OnGameEvent;
            _privilegedClientIds = new List<int>();
            _nextRequest = nextRequest;
        }

        /// <summary>
        /// Callback for the game event 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="gameEvent"></param>
        private void OnGameEvent(object sender, GameEvent gameEvent)
        {
            if (gameEvent.Type != EventType.ChangePermission || gameEvent.Extra is not EFClient.Permission perm)
            {
                return;
            }

            lock (_privilegedClientIds)
            {
                switch (perm)
                {
                    // we want to remove the claims when the client is demoted
                    case < EFClient.Permission.Trusted:
                    {
                        _privilegedClientIds.RemoveAll(id => id == gameEvent.Target.ClientId);
                        break;
                    }
                    // and add if promoted
                    case > EFClient.Permission.Trusted when !_privilegedClientIds.Contains(gameEvent.Target.ClientId):
                    {
                        _privilegedClientIds.Add(gameEvent.Target.ClientId);
                        break;
                    }
                }
            }
        }

        public async Task Invoke(HttpContext context)
        {
            // we want to load the initial list of privileged clients
            bool hasAny;
            lock (_privilegedClientIds)
            {
                hasAny = _privilegedClientIds.Any();
            }

            if (hasAny)
            {
                var ids = (await _manager.GetClientService().GetPrivilegedClients())
                    .Select(client => client.ClientId);

                lock (_privilegedClientIds)
                {
                    _privilegedClientIds.AddRange(ids);
                }
            }

            // sid stores the clientId
            var claimsId = context.User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Sid)?.Value;

            if (!string.IsNullOrEmpty(claimsId))
            {
                var clientId = int.Parse(claimsId);
                bool hasKey;
                lock (_privilegedClientIds)
                {
                    hasKey = _privilegedClientIds.Contains(clientId);
                }

                // they've been removed
                if (!hasKey && clientId != 1)
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }

            await _nextRequest.Invoke(context);
        }
    }
}
