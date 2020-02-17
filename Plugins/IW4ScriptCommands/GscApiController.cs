using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Text;

namespace WebfrontCore.Controllers.API
{
    [Route("api/gsc/[action]")]
    public class GscApiController : BaseController
    {
        public GscApiController(IManager manager) : base(manager)
        {

        }

        /// <summary>
        /// grabs basic info about the client from IW4MAdmin
        /// </summary>
        /// <param name="networkId"></param>
        /// <returns></returns>
        [HttpGet("{networkId}")]
        public IActionResult ClientInfo(string networkId)
        {
            long decimalNetworkId = networkId.ConvertGuidToLong(System.Globalization.NumberStyles.HexNumber);
            var clientInfo = Manager.GetActiveClients()
                .FirstOrDefault(c => c.NetworkId == decimalNetworkId);

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
    }
}
