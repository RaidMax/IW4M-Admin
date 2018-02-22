using Microsoft.AspNetCore.Mvc;
using SharedLibrary;
using SharedLibrary.Dtos;
using SharedLibrary.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers
{
    public class ClientController : Controller
    {
        public async Task<IActionResult> ProfileAsync(int id)
        {
            var client = await IW4MAdmin.ApplicationManager.GetInstance().GetClientService().Get(id);
            var clientDto = new PlayerInfo()
            {
                Name = client.Name,
                Level = client.Level.ToString(),
                ClientId = client.ClientId,
                IPAddress = client.IPAddressString,
                NetworkId = client.NetworkId,
                ConnectionCount = client.Connections,
                FirstSeen = Utilities.GetTimePassed(client.FirstConnection, false),
                LastSeen = Utilities.GetTimePassed(client.LastConnection, false),
                TimePlayed = Math.Round(client.TotalConnectionTime / 3600.0, 1).ToString("#,##0"),
                Meta = new List<ProfileMeta>(),
                Aliases = client.AliasLink.Children
                    .Where(a => a.Name != client.Name)
                    .Select(a => a.Name)
                    .Distinct()
                     .OrderBy(a => a)
                    .ToList(),
                IPs = client.AliasLink.Children
                    .Select(i => i.IPAddress.ConvertIPtoString())
                    .Distinct()
                    .OrderBy(i => i)
                    .ToList(),
            };

            clientDto.Meta.AddRange(await MetaService.GetMeta(client.ClientId));
            clientDto.Meta.AddRange(await IW4MAdmin.ApplicationManager.GetInstance().GetPenaltyService().ReadGetClientPenaltiesAsync(client.ClientId));
            clientDto.Meta.AddRange(await IW4MAdmin.ApplicationManager.GetInstance().GetPenaltyService().ReadGetClientPenaltiesAsync(client.ClientId, false));
            clientDto.Meta = clientDto.Meta.OrderByDescending(m => m.When).ToList();

            return View("Profile/Index", clientDto);
        }

        public async Task<IActionResult> PrivilegedAsync()
        {
            var admins = (await IW4MAdmin.ApplicationManager.GetInstance().GetClientService().GetPrivilegedClients())
                .Where(a => a.Active)
                .OrderByDescending(a => a.Level);
            var adminsDict = new Dictionary<SharedLibrary.Objects.Player.Permission, IList<ClientInfo>>();
            foreach (var admin in admins)
            {
                if (!adminsDict.ContainsKey(admin.Level))
                    adminsDict.Add(admin.Level, new List<ClientInfo>());

                adminsDict[admin.Level].Add(new ClientInfo()
                {
                    Name = admin.Name,
                    ClientId = admin.ClientId
                });
            }

            ViewBag.Title = "Current Privileged Users";
            return View("Privileged/Index", adminsDict);
        }


        public async Task<IActionResult> FindAsync(string clientName)
        {
            var clients = (await IW4MAdmin.ApplicationManager.GetInstance().GetClientService().GetClientByName(clientName))
                            .OrderByDescending(c => c.LastConnection);
            var clientsDto = clients.Select(c => new PlayerInfo()
            {
                Name = c.Name,
                Level = c.Level.ToString(),
                ClientId = c.ClientId,
                LastSeen = Utilities.GetTimePassed(c.LastConnection, false)
            })
            .ToList();

            ViewBag.Name = $"Clients Matching \"{clientName}\"";
            return View("Find/Index", clientsDto);
        }
    }
}
