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
            if (gameEvent.Type == EventType.ChangePermission &&
                gameEvent.Extra is EFClient.Permission perm)
            {
                // we want to remove the claims when the client is demoted
                if (perm < EFClient.Permission.Trusted)
                {
                    lock (_privilegedClientIds)
                    {
                        _privilegedClientIds.RemoveAll(id => id == gameEvent.Target.ClientId);
                    }
                }
                // and add if promoted
                else if (perm > EFClient.Permission.Trusted &&
                    !_privilegedClientIds.Contains(gameEvent.Target.ClientId))
                {
                    lock (_privilegedClientIds)
                    {
                        _privilegedClientIds.Add(gameEvent.Target.ClientId);
                    }
                }
            }
        }

        public async Task Invoke(HttpContext context)
        {
            // we want to load the initial list of privileged clients
            if (_privilegedClientIds.Count == 0)
            {
                var ids = (await _manager.GetClientService().GetPrivilegedClients())
                    .Select(_client => _client.ClientId);

                lock (_privilegedClientIds)
                {
                    _privilegedClientIds.AddRange(ids);
                }
            }

            // sid stores the clientId
            string claimsId = context.User.Claims.FirstOrDefault(_claim => _claim.Type == ClaimTypes.Sid)?.Value;

            if (!string.IsNullOrEmpty(claimsId))
            {
                int clientId = int.Parse(claimsId);
                // they've been removed
                if (!_privilegedClientIds.Contains(clientId) && clientId != 1)
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }

            await _nextRequest.Invoke(context);
        }
    }
}
