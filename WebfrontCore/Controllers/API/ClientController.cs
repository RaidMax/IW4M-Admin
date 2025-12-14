using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Services;
using WebfrontCore.Controllers.API.Dtos;
using WebfrontCore.Permissions;
using WebfrontCore.QueryHelpers.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Microsoft.Extensions.Options;
using SharedLibraryCore.QueryHelper;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// api controller for client operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController(
        ILogger<ClientController> logger,
        IResourceQueryHelper<FindClientRequest, FindClientResult> clientQueryHelper,
        IResourceQueryHelper<ClientResourceRequest, ClientResourceResponse> clientResourceHelper,
        ClientService clientService,
        IManager manager,
        IMetaServiceV2 metaService,
        IGeoLocationService geoLocationService,
        IInteractionRegistration interactionRegistration)
        : BaseController(manager)
    {
        private readonly ILogger _logger = logger;

        [HttpGet("find")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> FindAsync([FromQuery] FindClientRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Messages = ModelState.Values
                        .SelectMany(value => value.Errors.Select(error => error.ErrorMessage)).ToArray()
                });
            }

            try
            {
                var results = await clientQueryHelper.QueryResource(request);

                return Ok(new FindClientResponse
                {
                    TotalFoundClients = results.TotalResultCount,
                    Clients = results.Results
                });
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to retrieve clients with query - {@Request}", request);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse { Messages = [e.Message] });
            }
        }

        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchAsync([FromQuery] ClientResourceRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Messages = ModelState.Values
                        .SelectMany(value => value.Errors.Select(error => error.ErrorMessage)).ToArray()
                });
            }

            try
            {
                request.RequesterPermission = Client.Level;
                var results = await clientResourceHelper.QueryResource(request);

                return Ok(results);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to search clients with query - {@Request}", request);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse { Messages = [e.Message] });
            }
        }

        [HttpGet("privileged")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPrivilegedAsync()
        {
            if (Manager.GetApplicationSettings().Configuration().EnablePrivilegedUserPrivacy && !Authorized)
            {
                 return Forbid();
            }

            var admins = (await clientService.GetPrivilegedClients())
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

            return Ok(adminsDict);
        }

        [HttpGet("{clientId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPlayerInfoAsync([FromRoute] int clientId)
        {
            try
            {
                var clientInfo = await clientService.Get(clientId);
                if (clientInfo is null)
                {
                    return BadRequest("Could not find client");
                }

                var metaResult = await metaService
                    .GetPersistentMetaByLookup(EFMeta.ClientTagV2, EFMeta.ClientTagNameV2, clientInfo.ClientId);

                return Ok(new ClientInfoResult
                {
                    ClientId = clientInfo.ClientId,
                    Name = clientInfo.CleanedName,
                    Level = clientInfo.Level.ToLocalizedLevelName(),
                    NetworkId = clientInfo.NetworkId,
                    GameName = clientInfo.GameName.ToString(),
                    Tag = metaResult?.Value,
                    FirstConnection = clientInfo.FirstConnection,
                    LastConnection = clientInfo.LastConnection,
                    TotalConnectionTime = clientInfo.TotalConnectionTime,
                    Connections = clientInfo.Connections,
                });
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to retrieve information for Client - {ClientId}", clientId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse { Messages = [e.Message] });
            }
        }
        
        [HttpGet("{clientId:int}/profile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SharedLibraryCore.Dtos.PlayerInfo>> GetProfileAsync([FromRoute] int clientId, [FromQuery] MetaType? metaFilterType)
        {
             var client = await Manager.GetClientService().Get(clientId);

            if (client == null)
            {
                return NotFound();
            }

            var activePenalties = await Manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId,
                client.CurrentAliasId, client.NetworkId, client.GameName, client.IPAddress);

            var persistentMetaTask = new[]
            {
                metaService.GetPersistentMetaByLookup(EFMeta.ClientTagV2, EFMeta.ClientTagNameV2, client.ClientId),
                metaService.GetPersistentMeta("GravatarEmail", client.ClientId),
            };

            var persistentMeta = await Task.WhenAll(persistentMetaTask);
            var tag = persistentMeta[0];
            var gravatar = persistentMeta[1];
            var note = await metaService.GetPersistentMetaValue<ClientNoteMetaResponse>("ClientNotes", client.ClientId);

            if (tag?.Value != null)
            {
                client.SetAdditionalProperty(EFMeta.ClientTagV2, tag.Value);
            }

            if (!string.IsNullOrWhiteSpace(note?.Note))
            {
                note.OriginEntityName = await clientService.GetClientNameById(note.OriginEntityId);
            }

            var interactions =
                await interactionRegistration.GetInteractions("Webfront::Profile", clientId, client.GameName);

            var hasActiveBan = activePenalties.Any(penalty => penalty.Type == EFPenalty.PenaltyType.Ban);
            if (hasActiveBan)
            {
                client.Level = Data.Models.Client.EFClient.Permission.Banned;
            }

            var displayLevelInt = (int)client.Level;
            var displayLevel = client.Level.ToLocalizedLevelName();

            var shouldHideBanLevel = !hasActiveBan && client.Level == Data.Models.Client.EFClient.Permission.Banned;
            if (!Authorized && client.Level.ShouldHideLevel() || shouldHideBanLevel)
            {
                displayLevelInt = (int)Data.Models.Client.EFClient.Permission.User;
                displayLevel = Data.Models.Client.EFClient.Permission.User.ToLocalizedLevelName();
            }

            displayLevel = string.IsNullOrEmpty(client.Tag) ? displayLevel : $"{displayLevel} ({client.Tag})";
            var ingameClient = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == client.ClientId);

            var clientDto = new SharedLibraryCore.Dtos.PlayerInfo
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
                Meta = new List<SharedLibraryCore.Dtos.Meta.Responses.InformationResponse>(),
                Aliases = client.AliasLink.Children
                    .Select(alias => (alias.Name, alias.DateAdded))
                    .GroupBy(alias => alias.Name.StripColors())
                    .Select(grp => grp.OrderByDescending(item => item.Name.Length).First())
                    .Distinct()
                    .Select(a => new ProfileMetaEntry { Value = a.Name, Date = a.DateAdded })
                    .ToList(),
                IPs = PermissionsSet.HasPermission(WebfrontEntity.ClientIPAddress, WebfrontPermission.Read)
                    ? client.AliasLink.Children
                        .Select(alias => (alias.IPAddress.ConvertIPtoString(), alias.DateAdded))
                        .GroupBy(alias => alias.Item1)
                        .Select(grp => grp.OrderByDescending(item => item.DateAdded).First())
                        .Distinct()
                        .Select(i => new ProfileMetaEntry { Value = i.Item1, Date = i.DateAdded })
                        .ToList()
                    : new List<ProfileMetaEntry>(),
                HasActivePenalty = activePenalties.Any(penalty => penalty.Type != EFPenalty.PenaltyType.Flag),
                Online = ingameClient != null,
                TimeOnline = (DateTime.UtcNow - client.LastConnection).HumanizeForCurrentCulture(),
                LinkedAccounts = client.LinkedAccounts,
                MetaFilterType = metaFilterType,
                ConnectProtocolUrl = ingameClient?.CurrentServer.EventParser.URLProtocolFormat.FormatExt(
                    ingameClient.CurrentServer.ResolvedIpEndPoint.Address.IsInternal()
                        ? Program.Manager.ExternalIPAddress
                        : ingameClient.CurrentServer.ListenAddress,
                    ingameClient.CurrentServer.ListenPort),
                CurrentServerName = ingameClient?.CurrentServer?.Hostname,
                GeoLocationInfo = MapGeoLocation(await geoLocationService.Locate(client.IPAddressString)),
                NoteMeta = string.IsNullOrWhiteSpace(note?.Note) ? null: note,
                Interactions = interactions.Select(interaction => new InteractionInfo 
                {
                    EntityId = interaction.EntityId,
                    InteractionId = interaction.InteractionId,
                    InteractionType = interaction.InteractionType,
                    Enabled = interaction.Enabled,
                    Name = interaction.Name,
                    Description = interaction.Description,
                    DisplayMeta = interaction.DisplayMeta,
                    ActionPath = interaction.ActionPath,
                    ActionMeta = interaction.ActionMeta,
                    ActionUri = interaction.ActionUri,
                    MinimumPermission = interaction.MinimumPermission,
                    PermissionEntity = interaction.PermissionEntity,
                    PermissionAccess = interaction.PermissionAccess,
                    Source = interaction.Source
                }).ToList(),
            };

            var meta = await metaService.GetRuntimeMeta<SharedLibraryCore.Dtos.Meta.Responses.InformationResponse>(new ClientPaginationRequest
            {
                ClientId = client.ClientId,
                Before = DateTime.UtcNow
            }, MetaType.Information);

            if (gravatar != null)
            {
                clientDto.Meta.Add(new SharedLibraryCore.Dtos.Meta.Responses.InformationResponse()
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
            
            return clientDto;
        }

        [HttpGet("{clientId:int}/meta")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>>> GetMetaAsync([FromRoute] int clientId, [FromQuery] int count, [FromQuery] int offset, [FromQuery] long? startAt, [FromQuery] MetaType? metaType, CancellationToken token)
        {
            var request = new ClientPaginationRequest
            {
                ClientId = clientId,
                Count = count,
                Offset = offset,
                Before = startAt.HasValue ? DateTime.FromFileTimeUtc(startAt.Value) : DateTime.UtcNow,
            };

            var config = Manager.GetApplicationSettings().Configuration();
            var level = Authorized ? Data.Models.Client.EFClient.Permission.Owner : Data.Models.Client.EFClient.Permission.User;
            // TODO: Use actual user level if authenticated, but for now Authorized check is simple.
            // If we want real level, we need User UserClaimsPrincipal (if available in API)
            // But Authorized property in BaseController uses User.
            
            if (User.Identity.IsAuthenticated)
            {
                 var levelClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                 if (Enum.TryParse(levelClaim, out Data.Models.Client.EFClient.Permission result))
                 {
                     level = result;
                 }
            }

             if (!config.PermissionSets.TryGetValue(level.ToString(), out var permissionSet))
            {
                permissionSet = new List<string>();
            }

            IEnumerable<IClientMeta> meta;

            if (metaType is null or MetaType.All)
            {
                meta = await metaService.GetRuntimeMeta(request, token);
            }
            else
            {
                 meta = metaType switch
                {
                    MetaType.Information => await metaService.GetRuntimeMeta<SharedLibraryCore.Dtos.Meta.Responses.InformationResponse>(request, metaType.Value, token),
                    MetaType.AliasUpdate => permissionSet.HasPermission(WebfrontEntity.MetaAliasUpdate, WebfrontPermission.Read)
                        ? await metaService.GetRuntimeMeta<SharedLibraryCore.Dtos.Meta.Responses.UpdatedAliasResponse>(request, metaType.Value, token)
                        : new List<IClientMeta>(),
                    MetaType.ChatMessage => await metaService.GetRuntimeMeta<SharedLibraryCore.Dtos.Meta.Responses.MessageResponse>(request, metaType.Value, token),
                    MetaType.Penalized => await metaService.GetRuntimeMeta<SharedLibraryCore.Dtos.Meta.Responses.AdministeredPenaltyResponse>(request, metaType.Value, token),
                    MetaType.ReceivedPenalty => await metaService.GetRuntimeMeta<SharedLibraryCore.Dtos.Meta.Responses.ReceivedPenaltyResponse>(request, metaType.Value, token),
                    MetaType.ConnectionHistory => await metaService.GetRuntimeMeta<SharedLibraryCore.Dtos.Meta.Responses.ConnectionHistoryResponse>(request, metaType.Value, token),
                    MetaType.PermissionLevel => await metaService.GetRuntimeMeta<SharedLibraryCore.Dtos.Meta.Responses.PermissionLevelChangedResponse>(request, metaType.Value, token),
                    _ => await metaService.GetRuntimeMeta(request, token) // Fallback
                };
            }

            if (level < Data.Models.Client.EFClient.Permission.Trusted)
            {
                meta = meta?.Where(_meta => !_meta.IsSensitive);
            }

            return Ok(meta?.Cast<SharedLibraryCore.Dtos.Meta.Responses.BaseMetaResponse>().ToList());
        }

        [HttpPost("{clientId:int}/login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromRoute] int clientId, [FromBody, Required] PasswordRequest request)
        {
            if (clientId is 0)
            {
                return Unauthorized();
            }

            if (Authorized)
            {
                return Ok();
            }

            try
            {
                var privilegedClient = await clientService.GetClientForLogin(clientId);
                var loginSuccess = false;

                if (!Authorized)
                {
                    var tokenData = new TokenIdentifier
                    {
                        ClientId = clientId,
                        Token = request.Password
                    };

                    loginSuccess = Manager.TokenAuthenticator.AuthorizeToken(tokenData) ||
                                   (await Task.FromResult(Hashing.Hash(request.Password, privilegedClient.PasswordSalt)))[0] ==
                                   privilegedClient.Password;
                }

                if (loginSuccess)
                {
                    List<Claim> claims =
                    [
                        new Claim(ClaimTypes.NameIdentifier, privilegedClient.Name),
                        new Claim(ClaimTypes.Role, privilegedClient.Level.ToString()),
                        new Claim(ClaimTypes.Sid, privilegedClient.ClientId.ToString()),
                        new Claim(ClaimTypes.PrimarySid, privilegedClient.NetworkId.ToString("X")),
                        new Claim(ClaimTypes.PrimaryGroupSid, privilegedClient.GameName.ToString())
                    ];

                    var claimsIdentity = new ClaimsIdentity(claims, "login");
                    var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                    await SignInAsync(claimsPrinciple);

                    Manager.AddEvent(new GameEvent
                    {
                        Origin = privilegedClient,
                        Type = GameEvent.EventType.Login,
                        Owner = Manager.GetServers().First(),
                        Data = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var gameStringValues)
                            ? gameStringValues.ToString()
                            : HttpContext.Connection.RemoteIpAddress?.ToString()
                    });

                    Manager.QueueEvent(new LoginEvent
                    {
                        Source = this,
                        LoginSource = LoginEvent.LoginSourceType.Webfront,
                        EntityId = Client.ClientId.ToString(),
                        Identifier = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var loginStringValues)
                            ? loginStringValues.ToString()
                            : HttpContext.Connection.RemoteIpAddress?.ToString()
                    });

                    return Ok();
                }
            }
            catch (Exception)
            {
                return Unauthorized();
            }

            return Unauthorized();
        }

        [HttpPost("{clientId:int}/logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Logout()
        {
            if (Authorized)
            {
                Manager.AddEvent(new GameEvent
                {
                    Origin = Client,
                    Type = GameEvent.EventType.Logout,
                    Owner = Manager.GetServers().First(),
                    Data = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var gameStringValues)
                        ? gameStringValues.ToString()
                        : HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                Manager.QueueEvent(new LogoutEvent
                {
                    Source = this,
                    LoginSource = LoginEvent.LoginSourceType.Webfront,
                    EntityId = Client.ClientId.ToString(),
                    Identifier = HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var logoutStringValues)
                        ? logoutStringValues.ToString()
                        : HttpContext.Connection.RemoteIpAddress?.ToString()
                });
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }

        public class PasswordRequest
        {
            public string Password { get; set; }
        }
        
        private static SharedLibraryCore.Dtos.GeoLocationInfo MapGeoLocation(SharedLibraryCore.Interfaces.IGeoLocationResult geoLocation)
        {
            if (geoLocation == null)
            {
                return null;
            }
            
            return new SharedLibraryCore.Dtos.GeoLocationInfo
            {
                Country = geoLocation.Country,
                CountryCode = geoLocation.CountryCode,
                Region = geoLocation.Region,
                ASN = geoLocation.ASN,
                Timezone = geoLocation.Timezone,
                Organization = geoLocation.Organization
            };
        }
    }
}
