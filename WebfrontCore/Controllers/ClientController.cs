using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebfrontCore.ViewComponents;
using static SharedLibraryCore.Database.Models.EFClient;
using static SharedLibraryCore.Database.Models.EFPenalty;

namespace WebfrontCore.Controllers
{
    public class ClientController : BaseController
    {
        private readonly IMetaService _metaService;

        public ClientController(IManager manager, IMetaService metaService) : base(manager)
        {
            _metaService = metaService;
        }

        public async Task<IActionResult> ProfileAsync(int id, MetaType? metaFilterType)
        {
            var client = await Manager.GetClientService().Get(id);

            if (client == null)
            {
                return NotFound();
            }

            var activePenalties = (await Manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId, client.IPAddress));


            int displayLevelInt = (int)client.Level;
            string displayLevel = client.Level.ToLocalizedLevelName();

            if (!Authorized && client.Level.ShouldHideLevel())
            {
                displayLevelInt = (int)Permission.User;
                displayLevel = Permission.User.ToLocalizedLevelName();
            }

            var clientDto = new PlayerInfo()
            {
                Name = client.Name,
                Level = displayLevel,
                LevelInt = displayLevelInt,
                ClientId = client.ClientId,
                IPAddress = client.IPAddressString,
                NetworkId = client.NetworkId,
                Meta = new List<InformationResponse>(),
                Aliases = client.AliasLink.Children
                    .Select(_alias => _alias.Name)
                    .GroupBy(_alias => _alias.StripColors())
                    // we want the longest "duplicate" name
                    .Select(_grp => _grp.OrderByDescending(_name => _name.Length).First())
                    .Distinct()
                    .OrderBy(a => a)
                    .ToList(),
                IPs = client.AliasLink.Children
                    .Where(i => i.IPAddress != null)
                    .OrderByDescending(i => i.DateAdded)
                    .Select(i => i.IPAddress.ConvertIPtoString())
                    .Prepend(client.CurrentAlias.IPAddress.ConvertIPtoString())
                    .Distinct()
                    .ToList(),
                HasActivePenalty = activePenalties.Any(_penalty => _penalty.Type != PenaltyType.Flag),
                Online = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == client.ClientId) != null,
                TimeOnline = (DateTime.UtcNow - client.LastConnection).HumanizeForCurrentCulture(),
                LinkedAccounts = client.LinkedAccounts,
                MetaFilterType = metaFilterType
            };

            var meta = await _metaService.GetRuntimeMeta<InformationResponse>(new ClientPaginationRequest
            {
                ClientId = client.ClientId,
                Before = DateTime.UtcNow
            }, MetaType.Information);

            var gravatar = await _metaService.GetPersistentMeta("GravatarEmail", client);
            if (gravatar != null)
            {
                clientDto.Meta.Add(new InformationResponse()
                {
                    Key = "GravatarEmail",
                    Type = MetaType.Other,
                    Value = gravatar.Value
                });
            }

            clientDto.ActivePenalty = activePenalties.OrderByDescending(_penalty => _penalty.Type).FirstOrDefault();
            clientDto.Meta.AddRange(Authorized ? meta : meta.Where(m => !m.IsSensitive));

            string strippedName = clientDto.Name.StripColors();
            ViewBag.Title = strippedName.Substring(strippedName.Length - 1).ToLower()[0] == 's' ?
                strippedName + "'" :
                strippedName + "'s";
            ViewBag.Title += " " + Localization["WEBFRONT_CLIENT_PROFILE_TITLE"];
            ViewBag.Description = $"Client information for {strippedName}";
            ViewBag.Keywords = $"IW4MAdmin, client, profile, {strippedName}";

            return View("Profile/Index", clientDto);
        }

        public async Task<IActionResult> PrivilegedAsync()
        {
            var admins = (await Manager.GetClientService().GetPrivilegedClients())
                .OrderByDescending(_client => _client.Level)
                .ThenBy(_client => _client.Name);

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

            foreach (var client in clientsDto)
            {
                if (!Authorized && ((Permission)client.LevelInt).ShouldHideLevel())
                {
                    client.LevelInt = (int)Permission.User;
                    client.Level = Permission.User.ToLocalizedLevelName();
                }
            }

            ViewBag.Title = $"{clientsDto.Count} {Localization["WEBFRONT_CLIENT_SEARCH_MATCHING"]} \"{clientName}\"";
            return View("Find/Index", clientsDto);
        }

        public async Task<IActionResult> Meta(int id, int count, int offset, long? startAt, MetaType? metaFilterType)
        {
            var request = new ClientPaginationRequest
            {
                ClientId = id,
                Count = count,
                Offset = offset,
                Before = DateTime.FromFileTimeUtc(startAt ?? DateTime.UtcNow.ToFileTimeUtc())
            };

            var meta = await ProfileMetaListViewComponent.GetClientMeta(_metaService, metaFilterType, Client.Level, request);

            if (!meta.Any())
            {
                return Ok();
            }

            return View("Components/ProfileMetaList/_List", meta);
        }
    }
}
