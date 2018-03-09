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
    public class ClientController : BaseController
    {
        public async Task<IActionResult> ProfileAsync(int id)
        {
            var client = await Manager.GetClientService().Get(id);
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

            var meta = await MetaService.GetMeta(client.ClientId);
            var penaltyMeta = await Manager.GetPenaltyService()
                .ReadGetClientPenaltiesAsync(client.ClientId);
            var administeredPenaltiesMeta = await Manager.GetPenaltyService()
                .ReadGetClientPenaltiesAsync(client.ClientId, false);

            if (client.Level > SharedLibrary.Objects.Player.Permission.Trusted)
                clientDto.Meta.Add(new ProfileMeta()
                {
                    Key = "Masked",
                    Value = client.Masked ? "Is" : "Is not",
                    Sensitive = false,
                    When = DateTime.MinValue
                });

            clientDto.Meta.AddRange(Authorized ? meta : meta.Where(m => !m.Sensitive));
            clientDto.Meta.AddRange(Authorized ? penaltyMeta : penaltyMeta.Where(m => !m.Sensitive));
            clientDto.Meta.AddRange(Authorized ? administeredPenaltiesMeta : penaltyMeta.Where(m => !m.Sensitive));
            clientDto.Meta = clientDto.Meta
                .OrderByDescending(m => m.When)
                .ToList();

            ViewBag.Title = clientDto.Name;

            return View("Profile/Index", clientDto);
        }

        public async Task<IActionResult> PrivilegedAsync()
        {
            var admins = (await Manager.GetClientService().GetPrivilegedClients())
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
            var clients = (await Manager.GetClientService().GetClientByName(clientName))
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
