using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFClient;
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
            //_manager.OnServerEvent += OnGameEvent;
            _privilegedClientIds = new List<int>();
            _nextRequest = nextRequest;
        }

        /// <summary>
        /// Callback for the game event 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnGameEvent(object sender, GameEventArgs args)
        {
            if (args.Event.Type == EventType.ChangePermission &&
                args.Event.Extra is Permission perm)
            {
                // we want to remove the claims when the client is demoted
                if (perm < Permission.Trusted)
                {
                    lock (_privilegedClientIds)
                    {
                        _privilegedClientIds.RemoveAll(id => id == args.Event.Target.ClientId);
                    }
                }
                // and add if promoted
                else if (perm > Permission.Trusted &&
                    !_privilegedClientIds.Contains(args.Event.Target.ClientId))
                {
                    lock (_privilegedClientIds)
                    {
                        _privilegedClientIds.Add(args.Event.Target.ClientId);
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
                    await context.SignOutAsync();
                }
            }

            await _nextRequest.Invoke(context);
        }
    }
}
