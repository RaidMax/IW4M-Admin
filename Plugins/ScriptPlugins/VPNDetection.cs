#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// VPN Detection Plugin - checks connecting clients against a proxy/VPN lookup service and kicks
/// anyone using a VPN unless they have been whitelisted. Ported from the legacy VPNDetection.js
/// Jint script with a C#-first design (typed config, HttpClient, command classes, inlined EF query).
/// </summary>
public class VpnDetectionPlugin : IPluginV2
{
    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration("VPNDetectionSettings", new VpnDetectionConfiguration());
    }

    public string Name => "VPN Detection Plugin";
    public string Author => "RaidMax";
    public string Version => "2.1";

    private const string VpnWhitelistKey = "Webfront::Profile::VPNWhitelist";
    private const string VpnAllowListKey = "Webfront::Nav::Admin::VPNAllowList";

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly ILogger<VpnDetectionPlugin> _logger;
    private readonly VpnDetectionConfiguration _config;
    private readonly IInteractionRegistration _interactionRegistration;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ApplicationConfiguration _appConfig;

    public VpnDetectionPlugin(
        ILogger<VpnDetectionPlugin> logger,
        VpnDetectionConfiguration config,
        IInteractionRegistration interactionRegistration,
        IDatabaseContextFactory contextFactory,
        IManager manager)
    {
        _logger = logger;
        _config = config;
        _interactionRegistration = interactionRegistration;
        _contextFactory = contextFactory;
        _appConfig = manager.GetApplicationSettings().Configuration();

        IManagementEventSubscriptions.ClientStateAuthorized += OnClientAuthorized;

        RegisterInteractions();

        _logger.LogInformation("{Name} {Version} by {Author} loaded. Enabled={Enabled}, Whitelisted={Count}",
            Name, Version, Author, _config.Enabled, _config.VpnExceptionIds.Count);
    }

    private void RegisterInteractions()
    {
        _interactionRegistration.UnregisterInteraction(VpnWhitelistKey);
        _interactionRegistration.UnregisterInteraction(VpnAllowListKey);

        // Per-profile toggle button: whitelist a client, or disallow an already-whitelisted one.
        _interactionRegistration.RegisterInteraction(VpnWhitelistKey, (clientId, game, token) =>
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var interaction = new InteractionData
            {
                InteractionId = VpnWhitelistKey,
                ActionPath = "DynamicAction",
                EntityId = clientId,
                MinimumPermission = EFClient.Permission.Moderator,
                Source = Name,
                ActionMeta =
                {
                    ["InteractionId"] = "command",
                    ["ShouldRefresh"] = "true"
                }
            };

            if (clientId.HasValue && _config.VpnExceptionIds.Contains(clientId.Value))
            {
                interaction.Name = loc["WEBFRONT_VPN_BUTTON_DISALLOW"];
                interaction.DisplayMeta = "ph-x-circle";
                interaction.ActionMeta["Data"] = "disallowvpn";
                interaction.ActionMeta["ActionButtonLabel"] = loc["WEBFRONT_VPN_ACTION_DISALLOW_CONFIRM"];
                interaction.ActionMeta["Name"] = loc["WEBFRONT_VPN_ACTION_DISALLOW_TITLE"];
            }
            else
            {
                interaction.Name = loc["WEBFRONT_VPN_ACTION_ALLOW"];
                interaction.DisplayMeta = "ph-check-circle";
                interaction.ActionMeta["Data"] = "whitelistvpn";
                interaction.ActionMeta["ActionButtonLabel"] = loc["WEBFRONT_VPN_ACTION_ALLOW_CONFIRM"];
                interaction.ActionMeta["Name"] = loc["WEBFRONT_VPN_ACTION_ALLOW_TITLE"];
            }

            return Task.FromResult<IInteractionData>(interaction);
        });

        // Admin nav page listing whitelisted clients, each with a disallow button.
        _interactionRegistration.RegisterInteraction(VpnAllowListKey, (clientId, game, token) =>
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var interaction = new InteractionData
            {
                Name = loc["WEBFRONT_NAV_VPN_TITLE"],
                Description = loc["WEBFRONT_NAV_VPN_DESC"],
                DisplayMeta = "ph-check-circle",
                InteractionId = VpnAllowListKey,
                MinimumPermission = EFClient.Permission.Moderator,
                InteractionType = InteractionType.TemplateContent,
                Source = Name,
                Action = async (sourceId, targetId, g, meta, ct) =>
                {
                    var clients = await GetClientsDataAsync(_config.VpnExceptionIds, ct);

                    var disallowInteraction = new Dictionary<string, string>
                    {
                        ["InteractionId"] = "command",
                        ["Data"] = "disallowvpn",
                        ["ActionButtonLabel"] = loc["WEBFRONT_VPN_ACTION_DISALLOW_CONFIRM"],
                        ["Name"] = loc["WEBFRONT_VPN_ACTION_DISALLOW_TITLE"]
                    };
                    var encodedMeta = Uri.EscapeDataString(JsonSerializer.Serialize(disallowInteraction));

                    // Rendered inside the webfront's card container; follows WebfrontCore/REDESIGN-GUIDE.md.
                    var table = $@"<div class=""flex items-center gap-3 px-4 py-2.5 border-b border-line"">
                            <h2 class=""flex-1 text-[11px] font-semibold uppercase tracking-wider text-muted"">{loc["WEBFRONT_NAV_VPN_TITLE"]}</h2>
                            <span class=""font-mono text-[11px] px-1.5 py-1 rounded-full bg-surface-alt text-subtle tabular-nums"">{clients.Count}</span>
                        </div>";

                    if (clients.Count == 0)
                    {
                        table += @"<div class=""flex flex-col items-center justify-center gap-2 py-10 text-muted text-sm"">
                                <i class=""ph ph-shield-check text-3xl text-secondary""></i>
                                <span>No players are whitelisted.</span>
                            </div>";
                    }
                    else
                    {
                        table += @"<div class=""overflow-x-auto""><table class=""w-full text-sm"">
                            <thead><tr class=""text-[10px] font-semibold uppercase tracking-wider text-muted"">
                                <th class=""text-left px-4 py-2.5 border-b border-line"">Player</th>
                                <th class=""text-left px-4 py-2.5 border-b border-line"">Client ID</th>
                                <th class=""text-right px-4 py-2.5 border-b border-line""></th>
                            </tr></thead><tbody class=""divide-y divide-line"">";

                        foreach (var client in clients)
                        {
                            var cleanName = System.Net.WebUtility.HtmlEncode(client.Name.StripColors());
                            table += $@"<tr class=""hover:bg-surface-hover transition-colors"">
                                    <td class=""px-4 py-2.5 align-middle"">
                                        <a href=""/client/{client.ClientId}"" class=""font-semibold text-foreground hover:text-primary transition-colors"">{cleanName}</a>
                                    </td>
                                    <td class=""px-4 py-2.5 align-middle font-mono tabular-nums text-muted"">#{client.ClientId}</td>
                                    <td class=""px-4 py-2.5 align-middle text-right"">
                                        <button type=""button"" class=""profile-action inline-flex items-center gap-1.5 h-8 px-3 rounded-lg border border-error/40 bg-surface-alt text-xs font-semibold text-error hover:bg-error/10 transition-colors""
                                                data-action=""DynamicAction"" data-action-id=""{client.ClientId}"" data-action-meta=""{encodedMeta}"">
                                            <i class=""ph ph-x-circle text-base""></i>
                                            <span>{loc["WEBFRONT_VPN_BUTTON_DISALLOW"]}</span>
                                        </button>
                                    </td>
                                </tr>";
                        }

                        table += "</tbody></table></div>";
                    }

                    return table;
                }
            };

            return Task.FromResult<IInteractionData>(interaction);
        });
    }

    private Task OnClientAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        if (clientEvent.Client.IsBot || !_config.Enabled)
        {
            return Task.CompletedTask;
        }

        return CheckForVpnAsync(clientEvent.Client, token);
    }

    private async Task CheckForVpnAsync(EFClient origin, CancellationToken token)
    {
        if (_config.VpnExceptionIds.Contains(origin.ClientId))
        {
            _logger.LogInformation("{Origin} is whitelisted, so we are not checking VPN status", origin.ToString());
            return;
        }

        if (origin.IPAddressString is null)
        {
            _logger.LogDebug("{Client} does not have an IP address yet, so we are not checking their VPN status",
                origin.ToString());
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.xdefcon.com/proxy/check/?ip={origin.IPAddressString}");
            request.Headers.UserAgent.ParseAdd($"IW4MAdmin-{_appConfig.Id}");

            using var response = await HttpClient.SendAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token);

            bool usingVpn;
            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                usingVpn = root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True
                           && root.TryGetProperty("proxy", out var proxy) && proxy.ValueKind == JsonValueKind.True;
            }
            catch
            {
                _logger.LogWarning("There was a problem checking client IP ({IP}) for VPN - {Message}",
                    origin.IPAddressString, body);
                return;
            }

            if (!usingVpn)
            {
                _logger.LogDebug("{Client} is not using a VPN", origin.ToString());
                return;
            }

            _logger.LogInformation("{Origin} is using a VPN ({IP})", origin.ToString(), origin.IPAddressString);

            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var additionalInfo = string.IsNullOrEmpty(_appConfig.ContactUri)
                ? string.Empty
                : loc["SERVER_KICK_VPNS_NOTALLOWED_INFO"] + " " + _appConfig.ContactUri;
            var message = (loc["SERVER_KICK_VPNS_NOTALLOWED"] + " " + additionalInfo).TrimEnd();

            origin.Kick(message, origin.CurrentServer.AsConsoleClient());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "There was a problem checking client IP ({IP}) for VPN", origin.IPAddressString);
        }
    }

    private async Task<List<ClientWhitelistEntry>> GetClientsDataAsync(IReadOnlyCollection<int> clientIds,
        CancellationToken token)
    {
        if (clientIds.Count == 0)
        {
            return [];
        }

        var ids = clientIds.ToList();
        await using var context = _contextFactory.CreateContext(false);
        return await context.Clients
            .Where(client => ids.Contains(client.ClientId))
            .Select(client => new ClientWhitelistEntry(client.ClientId, client.CurrentAlias.Name))
            .ToListAsync(token);
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.ClientStateAuthorized -= OnClientAuthorized;
        _interactionRegistration.UnregisterInteraction(VpnWhitelistKey);
        _interactionRegistration.UnregisterInteraction(VpnAllowListKey);
        _logger.LogInformation("{Name} unloaded", Name);
    }

    private sealed record ClientWhitelistEntry(int ClientId, string Name);
}

/// <summary>
/// Whitelists a player's client id from VPN detection.
/// Usage: !whitelistvpn &lt;player&gt;
/// </summary>
public class WhitelistVpnCommand : Command
{
    private readonly VpnDetectionConfiguration _config;
    private readonly IConfigurationHandlerV2<VpnDetectionConfiguration> _configHandler;

    public WhitelistVpnCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        VpnDetectionConfiguration scriptConfig, IConfigurationHandlerV2<VpnDetectionConfiguration> configHandler)
        : base(config, translationLookup)
    {
        Name = "whitelistvpn";
        Description = "whitelists a player's client id from VPN detection";
        Alias = "wv";
        Permission = EFClient.Permission.SeniorAdmin;
        RequiresTarget = true;
        Arguments =
        [
            new CommandArgument { Name = "player", Required = true }
        ];
        _config = scriptConfig;
        _configHandler = configHandler;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var targetId = gameEvent.Target.ClientId;
        if (!_config.VpnExceptionIds.Contains(targetId))
        {
            _config.VpnExceptionIds.Add(targetId);
            await _configHandler.Set(_config);
        }

        gameEvent.Origin.Tell($"Successfully whitelisted {gameEvent.Target.Name}");
    }
}

/// <summary>
/// Disallows a player from connecting with a VPN (removes them from the whitelist).
/// Usage: !disallowvpn &lt;player&gt;
/// </summary>
public class DisallowVpnCommand : Command
{
    private readonly VpnDetectionConfiguration _config;
    private readonly IConfigurationHandlerV2<VpnDetectionConfiguration> _configHandler;

    public DisallowVpnCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        VpnDetectionConfiguration scriptConfig, IConfigurationHandlerV2<VpnDetectionConfiguration> configHandler)
        : base(config, translationLookup)
    {
        Name = "disallowvpn";
        Description = "disallows a player from connecting with a VPN";
        Alias = "dv";
        Permission = EFClient.Permission.SeniorAdmin;
        RequiresTarget = true;
        Arguments =
        [
            new CommandArgument { Name = "player", Required = true }
        ];
        _config = scriptConfig;
        _configHandler = configHandler;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var targetId = gameEvent.Target.ClientId;
        if (_config.VpnExceptionIds.RemoveAll(id => id == targetId) > 0)
        {
            await _configHandler.Set(_config);
        }

        gameEvent.Origin.Tell($"Successfully disallowed {gameEvent.Target.Name} from connecting with VPN");
    }
}

/// <summary>
/// Configuration for the VPN Detection plugin.
/// </summary>
public class VpnDetectionConfiguration
{
    /// <summary>
    /// Indicates whether VPN checking is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Client ids that are exempt from VPN detection.
    /// </summary>
    public List<int> VpnExceptionIds { get; set; } = new();
}
