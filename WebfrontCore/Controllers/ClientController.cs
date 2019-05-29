using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFPenalty;

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

            var clientDto = new PlayerInfo()
            {
                Name = client.Name,
                Level = client.Level.ToLocalizedLevelName(),
                LevelInt = (int)client.Level,
                ClientId = client.ClientId,
                IPAddress = client.IPAddressString,
                NetworkId = client.NetworkId,
                Meta = new List<ProfileMeta>(),
                Aliases = client.AliasLink.Children
                    .Where(a => a.Name != client.Name)
                    .Select(a => a.Name)
                    .Distinct()
                    .OrderBy(a => a)
                    .ToList(),
                IPs = client.AliasLink.Children
                    .Select(i => i.IPAddress.ConvertIPtoString())
                    .Union(new List<string>() { client.CurrentAlias.IPAddress.ConvertIPtoString() })
                    .Where(i => !string.IsNullOrEmpty(i))
                    .Distinct()
                    .OrderBy(i => i)
                    .ToList(),
                HasActivePenalty = activePenalties.Count > 0,
                ActivePenaltyType = activePenalties.Count > 0 ? activePenalties.First().Type.ToString() : null,
                Online = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == client.ClientId) != null,
                TimeOnline = (DateTime.UtcNow - client.LastConnection).TimeSpanText(),
                LinkedAccounts = client.LinkedAccounts
            };

            var meta = await MetaService.GetRuntimeMeta(client.ClientId, 0, 1, DateTime.UtcNow);
            var gravatar = await new MetaService().GetPersistentMeta("GravatarEmail", client);
            if (gravatar != null)
            {
                clientDto.Meta.Add(new ProfileMeta()
                {
                    Key = "GravatarEmail",
                    Type = ProfileMeta.MetaType.Other,
                    Value = gravatar.Value
                });
            }

            var currentPenalty = activePenalties.FirstOrDefault();

            if (currentPenalty != null && currentPenalty.Type == PenaltyType.TempBan)
            {
                clientDto.Meta.Add(new ProfileMeta()
                {
                    Key = Localization["WEBFRONT_CLIENT_META_REMAINING_BAN"],
                    Value = ((currentPenalty.Expires - DateTime.UtcNow) ?? new TimeSpan()).TimeSpanText(),
                    When = currentPenalty.When
                });
            }

            clientDto.Meta.AddRange(Authorized ? meta : meta.Where(m => !m.Sensitive));

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
                .GroupBy(a => a.AliasLinkId)
                .Select(_client => _client.OrderByDescending(_c => _c.LastConnection).First())
                .OrderByDescending(_client => _client.Level);

            var adminsDict = new Dictionary<EFClient.Permission, IList<ClientInfo>>();

            foreach (var admin in admins)
            {
                if (!adminsDict.ContainsKey(admin.Level))
                {
                    adminsDict.Add(admin.Level, new List<ClientInfo>());
                }

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
            if (string.IsNullOrWhiteSpace(clientName))
            {
                return StatusCode(400);
            }

            var clientsDto = await Manager.GetClientService().FindClientsByIdentifier(clientName);

            ViewBag.Title = $"{clientsDto.Count} {Localization["WEBFRONT_CLIENT_SEARCH_MATCHING"]} \"{clientName}\"";
            return View("Find/Index", clientsDto);
        }

        public async Task<IActionResult> Meta(int id, int count, int offset, DateTime? startAt)
        {
            IEnumerable<ProfileMeta> meta = await MetaService.GetRuntimeMeta(id, startAt == null ? offset : 0, count, startAt ?? DateTime.UtcNow);

            if (!Authorized)
            {
                meta = meta.Where(_meta => !_meta.Sensitive);
            }
            
            if (meta.Count() == 0)
            {
                return Ok();
            }

            return View("Components/ProfileMetaList/_List", meta);
        }
    }
}
