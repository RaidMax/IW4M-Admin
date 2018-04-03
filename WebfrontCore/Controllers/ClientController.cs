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
                LevelInt = (int)client.Level,
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

            if (Authorized && client.Level > SharedLibrary.Objects.Player.Permission.Trusted)
                clientDto.Meta.Add(new ProfileMeta()
                {
                    Key = "Masked",
                    Value = client.Masked ? "Is" : "Is not",
                    Sensitive = true,
                    When = DateTime.MinValue
                });

            if (Authorized)
            {
                clientDto.Meta.AddRange(client.AliasLink.Children
                    .GroupBy(a => a.Name)
                    .Select(a => a.First())
                    .Select(a => new ProfileMeta()
                    {
                        Key = "AliasEvent",
                        Value = $"Connected with name {a.Name}",
                        Sensitive = true,
                        When = a.DateAdded
                    }));
            }

            clientDto.Meta.AddRange(Authorized ? meta : meta.Where(m => !m.Sensitive));
            clientDto.Meta.AddRange(Authorized ? penaltyMeta : penaltyMeta.Where(m => !m.Sensitive));
            clientDto.Meta.AddRange(Authorized ? administeredPenaltiesMeta : administeredPenaltiesMeta.Where(m => !m.Sensitive));
            clientDto.Meta = clientDto.Meta
                .OrderByDescending(m => m.When)
                .ToList();

            ViewBag.Title = clientDto.Name.Substring(clientDto.Name.Length - 1).ToLower()[0] == 's' ?
                clientDto.Name + "'" :
                clientDto.Name + "'s";
            ViewBag.Title += " Profile";
            ViewBag.Description = $"Client information for {clientDto.Name}";
            ViewBag.Keywords = $"IW4MAdmin, client, profile, {clientDto.Name}";

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

            ViewBag.Title = "Privileged Clients";
            ViewBag.Description = "List of all privileged clients on IW4MAdmin";
            ViewBag.Keywords = "IW4MAdmin, privileged, admins, clients, administrators";

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
                LevelInt = (int)c.Level,
                ClientId = c.ClientId,
                LastSeen = Utilities.GetTimePassed(c.LastConnection, false)
            })
            .ToList();

            ViewBag.Title = $"{clientsDto.Count} Clients Matching \"{clientName}\"";
            return View("Find/Index", clientsDto);
        }
    }
}
