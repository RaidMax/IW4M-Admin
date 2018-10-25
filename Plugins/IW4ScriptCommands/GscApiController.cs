using IW4ScriptCommands.Commands;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers.API
{
    [Route("api/gsc/[action]")]
    public class GscApiController : ApiController
    {
        [HttpGet("{networkId}")]
        public IActionResult ClientInfo(string networkId)
        {
            var clientInfo = Manager.GetActiveClients()
                .FirstOrDefault(c => c.NetworkId == networkId.ConvertLong());

            if (clientInfo != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"admin={clientInfo.IsPrivileged()}");
                sb.AppendLine($"level={(int)clientInfo.Level}");
                sb.AppendLine($"levelstring={clientInfo.Level.ToLocalizedLevelName()}");
                sb.AppendLine($"connections={clientInfo.Connections}");
                sb.AppendLine($"authenticated={clientInfo.GetAdditionalProperty<bool>("IsLoggedIn") == true}");

                return Content(sb.ToString());
            }

            return Content("");
        }

        [HttpGet("{networkId}")]
        public IActionResult GetTeamAssignments(string networkId, int serverId, string teams = "", bool isDisconnect = false)
        {
            return Unauthorized();

            var client = Manager.GetActiveClients()
                .FirstOrDefault(c => c.NetworkId == networkId.ConvertLong());

            var server = Manager.GetServers().First(c => c.GetHashCode() == serverId);

            teams = teams ?? string.Empty;

            string assignments = Balance.GetTeamAssignments(client,  isDisconnect, server, teams);

            return Content(assignments);
        }
    }
}
