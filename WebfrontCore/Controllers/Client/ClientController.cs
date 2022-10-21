using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using SharedLibraryCore.Services;
using Stats.Config;
using WebfrontCore.Permissions;
using WebfrontCore.ViewComponents;

namespace WebfrontCore.Controllers
{
    public class ClientController : BaseController
    {
        private readonly IMetaServiceV2 _metaService;
        private readonly StatsConfiguration _config;
        private readonly IGeoLocationService _geoLocationService;
        private readonly ClientService _clientService;
        private readonly IInteractionRegistration _interactionRegistration;

        public ClientController(IManager manager, IMetaServiceV2 metaService, StatsConfiguration config,
            IGeoLocationService geoLocationService, ClientService clientService, IInteractionRegistration interactionRegistration) : base(manager)
        {
            _metaService = metaService;
            _config = config;
            _geoLocationService = geoLocationService;
            _clientService = clientService;
            _interactionRegistration = interactionRegistration;
        }

        [Obsolete]
        public IActionResult ProfileAsync(int id, MetaType? metaFilterType,
            CancellationToken token = default) => RedirectToAction("Profile", "Client", new
            { id, metaFilterType });

        public async Task<IActionResult> Profile(int id, MetaType? metaFilterType, CancellationToken token = default)
        {
            var client = await Manager.GetClientService().Get(id);

            if (client == null)
            {
                return NotFound();
            }

            var activePenalties = await Manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId,
                client.CurrentAliasId, client.NetworkId, client.GameName, client.IPAddress);

            var persistentMetaTask = new[]
            {
                _metaService.GetPersistentMetaByLookup(EFMeta.ClientTagV2, EFMeta.ClientTagNameV2, client.ClientId,
                    token),
                _metaService.GetPersistentMeta("GravatarEmail", client.ClientId, token),
            };

            var persistentMeta = await Task.WhenAll(persistentMetaTask);
            var tag = persistentMeta[0];
            var gravatar = persistentMeta[1];
            var note = await _metaService.GetPersistentMetaValue<ClientNoteMetaResponse>("ClientNotes", client.ClientId,
                token);

            if (tag?.Value != null)
            {
                client.SetAdditionalProperty(EFMeta.ClientTagV2, tag.Value);
            }

            if (!string.IsNullOrWhiteSpace(note?.Note))
            {
                note.OriginEntityName = await _clientService.GetClientNameById(note.OriginEntityId);
            }

            var interactions =
                await _interactionRegistration.GetInteractions("Webfront::Profile", id, client.GameName, token);

            // even though we haven't set their level to "banned" yet
            // (ie they haven't reconnected with the infringing player identifier)
            // we want to show them as banned as to not confuse people.
            var hasActiveBan = activePenalties.Any(penalty => penalty.Type == EFPenalty.PenaltyType.Ban);
            if (hasActiveBan)
            {
                client.Level = Data.Models.Client.EFClient.Permission.Banned;
            }

            var displayLevelInt = (int)client.Level;
            var displayLevel = client.Level.ToLocalizedLevelName();

            // if a linked ban has been revoked but they haven't reconnected, we should not show them as still banned
            var shouldHideBanLevel = !hasActiveBan && client.Level == Data.Models.Client.EFClient.Permission.Banned;
            if (!Authorized && client.Level.ShouldHideLevel() || shouldHideBanLevel)
            {
                displayLevelInt = (int)Data.Models.Client.EFClient.Permission.User;
                displayLevel = Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName();
            }

            displayLevel = string.IsNullOrEmpty(client.Tag) ? displayLevel : $"{displayLevel} ({client.Tag})";
            var ingameClient = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == client.ClientId);

            var clientDto = new PlayerInfo
            {
                Name = client.Name,
                Game = client.GameName,
                Level = displayLevel,
                LevelInt = displayLevelInt,
                ClientId = client.ClientId,
                IPAddress = PermissionsSet.HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read)
                    ? client.IPAddressString
                    : null,
                NetworkId = client.NetworkId,
                Meta = new List<InformationResponse>(),
                Aliases = client.AliasLink.Children
                    .Select(alias => (alias.Name, alias.DateAdded))
                    .GroupBy(alias => alias.Name.StripColors())
                    // we want the longest "duplicate" name
                    .Select(grp => grp.OrderByDescending(item => item.Name.Length).First())
                    .Distinct()
                    .ToList(),
                IPs = PermissionsSet.HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read)
                    ? client.AliasLink.Children
                        .Select(alias => (alias.IPAddress.ConvertIPtoString(), alias.DateAdded))
                        .GroupBy(alias => alias.Item1)
                        .Select(grp => grp.OrderByDescending(item => item.DateAdded).First())
                        .Distinct()
                        .ToList()
                    : new List<(string, DateTime)>(),
                HasActivePenalty = activePenalties.Any(penalty => penalty.Type != EFPenalty.PenaltyType.Flag),
                Online = ingameClient != null,
                TimeOnline = (DateTime.UtcNow - client.LastConnection).HumanizeForCurrentCulture(),
                LinkedAccounts = client.LinkedAccounts,
                MetaFilterType = metaFilterType,
                ConnectProtocolUrl = ingameClient?.CurrentServer.EventParser.URLProtocolFormat.FormatExt(
                    ingameClient.CurrentServer.ResolvedIpEndPoint.Address.IsInternal()
                        ? Program.Manager.ExternalIPAddress
                        : ingameClient.CurrentServer.IP,
                    ingameClient.CurrentServer.Port),
                CurrentServerName = ingameClient?.CurrentServer?.Hostname,
                GeoLocationInfo = await _geoLocationService.Locate(client.IPAddressString),
                NoteMeta = string.IsNullOrWhiteSpace(note?.Note) ? null: note,
                Interactions = interactions.ToList()
            };

            var meta = await _metaService.GetRuntimeMeta<InformationResponse>(new ClientPaginationRequest
            {
                ClientId = client.ClientId,
                Before = DateTime.UtcNow
            }, MetaType.Information);

            if (gravatar != null)
            {
                clientDto.Meta.Add(new InformationResponse()
                {
                    Key = "GravatarEmail",
                    Type = MetaType.Other,
                    Value = gravatar.Value
                });
            }

            clientDto.ActivePenalty = activePenalties.MaxBy(penalty => penalty.Type switch
            {
                EFPenalty.PenaltyType.TempMute => 0,
                EFPenalty.PenaltyType.Mute => 1,
                _ => (int)penalty.Type
            });
            clientDto.Meta.AddRange(Authorized ? meta : meta.Where(m => !m.IsSensitive));

            var strippedName = clientDto.Name.StripColors();
            ViewBag.Title = $"{strippedName} | {Localization["WEBFRONT_CLIENT_PROFILE_TITLE"]}";
            ViewBag.Description = Localization["WEBFRONT_PROFILE_DESCRIPTION"].FormatExt(strippedName);
            ViewBag.UseNewStats = _config?.EnableAdvancedMetrics ?? true;

            return View("Profile/Index", clientDto);
        }

        public async Task<IActionResult> Privileged()
        {
            if (Manager.GetApplicationSettings().Configuration().EnablePrivilegedUserPrivacy && !Authorized)
            {
                return RedirectToAction("Index", "Home");
            }

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

                adminsDict[admin.Level].Add(new ClientInfo
                {
                    Name = admin.Name,
                    ClientId = admin.ClientId,
                    LastConnection = admin.LastConnection,
                    IsMasked = admin.Masked,
                    Game = admin.GameName
                });
            }

            ViewBag.Title = Localization["WEBFRONT_CLIENT_PRIVILEGED_TITLE"];
            ViewBag.Description = Localization["WEBFRONT_DESCRIPTION_PRIVILEGED"];
            ViewBag.Keywords = Localization["WEBFRONT_KEYWORDS_PRIVILEGED"];

            return View("Privileged/Index", adminsDict);
        }

        public async Task<IActionResult> Find(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
            {
                return StatusCode(400);
            }

            var clientsDto = await Manager.GetClientService().FindClientsByIdentifier(clientName);

            foreach (var client in clientsDto)
            {
                if (!Authorized && ((Data.Models.Client.EFClient.Permission)client.LevelInt).ShouldHideLevel())
                {
                    client.LevelInt = (int)Data.Models.Client.EFClient.Permission.User;
                    client.Level = Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName();
                }
            }

            ViewBag.SearchTerm = clientName;
            ViewBag.ResultCount = clientsDto.Count;
            ViewBag.Title = Localization["WEBFRONT_SEARCH_RESULTS_TITLE"];
            
            return View("Find/Index", clientsDto);
        }

        public IActionResult Meta(int id, int count, int offset, long? startAt, MetaType? metaFilterType,
            CancellationToken token)
        {
            var request = new ClientPaginationRequest
            {
                ClientId = id,
                Count = count,
                Offset = offset,
                Before = DateTime.FromFileTimeUtc(startAt ?? DateTime.UtcNow.ToFileTimeUtc())
            };

            return ViewComponent(typeof(ProfileMetaListViewComponent), new
            {
                clientId = request.ClientId,
                count = request.Count,
                offset = request.Offset,
                startAt = request.Before,
                metaType = metaFilterType,
                token
            });
        }
    }
}
