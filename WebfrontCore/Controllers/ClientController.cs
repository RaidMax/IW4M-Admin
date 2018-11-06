using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SharedLibraryCore.Objects.Penalty;

namespace WebfrontCore.Controllers
{
    public class ClientController : BaseController
    {
        public async Task<IActionResult> ProfileAsync(int id)
        {
            var client = await Manager.GetClientService().Get(id);
            if (client == null)
            {
                return NotFound();
            }

            var activePenalties = await Manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId, client.IPAddress);

#if DEBUG
            Authorized = true;
#endif

            var clientDto = new PlayerInfo()
            {
                Name = client.Name,
                Level = client.Level.ToLocalizedLevelName(),
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
                HasActivePenalty = activePenalties.Count > 0,
                Online = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == client.ClientId) != null,
                TimeOnline = (DateTime.UtcNow - client.LastConnection).TimeSpanText(),
                LinkedAccounts = client.LinkedAccounts
            };

            var meta = await MetaService.GetMeta(client.ClientId);
            var penaltyMeta = await Manager.GetPenaltyService()
                .ReadGetClientPenaltiesAsync(client.ClientId);
            var administeredPenaltiesMeta = await Manager.GetPenaltyService()
                .ReadGetClientPenaltiesAsync(client.ClientId, false);

            if (Authorized && client.Level > EFClient.Permission.Trusted)
                clientDto.Meta.Add(new ProfileMeta()
                {
                    Key = Localization["WEBFRONT_CLIENT_META_MASKED"],
                    Value = client.Masked ? Localization["WEBFRONT_CLIENT_META_TRUE"] : Localization["WEBFRONT_CLIENT_META_FALSE"],
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
                        Value = $"{Localization["WEBFRONT_CLIENT_META_JOINED"]} {a.Name}",
                        Sensitive = true,
                        When = a.DateAdded
                    }));
            }

            if (Authorized)
            {
                penaltyMeta.ForEach(p => p.Value.Offense = p.Value.AutomatedOffense ?? p.Value.Offense);
                administeredPenaltiesMeta.ForEach(p => p.Value.Offense = p.Value.AutomatedOffense ?? p.Value.Offense);
            }

            clientDto.Meta.AddRange(Authorized ? meta : meta.Where(m => !m.Sensitive));
            clientDto.Meta.AddRange(Authorized ? penaltyMeta : penaltyMeta.Where(m => !m.Sensitive));
            clientDto.Meta.AddRange(Authorized ? administeredPenaltiesMeta : administeredPenaltiesMeta.Where(m => !m.Sensitive));
            clientDto.Meta.AddRange(client.Meta.Select(m => new ProfileMeta()
            {
                When = m.Created,
                Key = m.Key,
                Value = m.Value,
                Show = false,
            }));
            clientDto.Meta = clientDto.Meta
                .OrderByDescending(m => m.When)
                .ToList();

            ViewBag.Title = clientDto.Name.Substring(clientDto.Name.Length - 1).ToLower()[0] == 's' ?
                clientDto.Name + "'" :
                clientDto.Name + "'s";
            ViewBag.Title += " " + Localization["WEBFRONT_CLIENT_PROFILE_TITLE"];
            ViewBag.Description = $"Client information for {clientDto.Name}";
            ViewBag.Keywords = $"IW4MAdmin, client, profile, {clientDto.Name}";

            return View("Profile/Index", clientDto);
        }

        public async Task<IActionResult> PrivilegedAsync()
        {
            var admins = (await Manager.GetClientService().GetPrivilegedClients())
                .OrderByDescending(a => a.Level)
                .GroupBy(a => a.LinkId).Select(a => a.First());

            var adminsDict = new Dictionary<EFClient.Permission, IList<ClientInfo>>();

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

            ViewBag.Title = Localization["WEBFRONT_CLIENT_PRIVILEGED_TITLE"];
            ViewBag.Description = "List of all privileged clients on IW4MAdmin";
            ViewBag.Keywords = "IW4MAdmin, privileged, admins, clients, administrators";

            return View("Privileged/Index", adminsDict);
        }


        public async Task<IActionResult> FindAsync(string clientName)
        {
            var clients = (await Manager.GetClientService().FindClientsByIdentifier(clientName))
                            .OrderByDescending(c => c.LastConnection);

            var clientsDto = clients.Select(c => new PlayerInfo()
            {
                Name = c.Name,
                Level = c.Level.ToLocalizedLevelName(),
                LevelInt = (int)c.Level,
                ClientId = c.ClientId,
                LastSeen = Utilities.GetTimePassed(c.LastConnection, false)
            })
            .ToList();

            ViewBag.Title = $"{clientsDto.Count} {Localization["WEBFRONT_CLIENT_SEARCH_MATCHING"]} \"{clientName}\"";
            return View("Find/Index", clientsDto);
        }
    }
}
