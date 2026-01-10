#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.10.1

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Subnet Banlist Plugin - Bans clients based on IP subnet ranges (CIDR notation).
/// Allows banning entire subnets and checks clients on authorization.
/// </summary>
public class SubnetBanPlugin : IPluginV2
{
    public string Name => "Subnet Banlist Plugin";
    public string Author => "RaidMax";
    public string Version => "2.0";

    private const string SubnetBanlistKey = "Webfront::Nav::Admin::SubnetBanlist";
    private static readonly Regex CidrRegex = new(@"^([0-9]{1,3}\.){3}[0-9]{1,3}(\/([0-9]|[1-2][0-9]|3[0-2]))?$", RegexOptions.Compiled);

    private readonly ILogger<SubnetBanPlugin> _logger;
    private readonly ICsScriptPluginConfiguration _config;
    private readonly IInteractionRegistration _interactionRegistration;
    private readonly List<string> _subnetList = new();

    private string _banMessage = "You are not allowed to join this server.";

    public SubnetBanPlugin(
        ILogger<SubnetBanPlugin> logger,
        ICsScriptPluginConfiguration config,
        IInteractionRegistration interactionRegistration)
    {
        _logger = logger;
        _config = config;
        _interactionRegistration = interactionRegistration;

        // Subscribe to client authorization events
        IManagementEventSubscriptions.ClientStateAuthorized += OnClientAuthorized;

        // Load configuration
        LoadConfiguration();

        // Register webfront interaction
        RegisterInteraction();

        _logger.LogInformation("Subnet Ban loaded");
    }

    private void LoadConfiguration()
    {
        ReloadSubnetList();
        ReloadBanMessage();

        // Subscribe to config updates to reload subnet list when changed
        _config.GetValue("SubnetBanList", new List<object>(), updatedValue =>
        {
            if (updatedValue is List<object> newList)
            {
                ReloadSubnetList();
            }
        });

        _config.GetValue("BanMessage", _banMessage, updatedValue =>
        {
            if (updatedValue is string newMessage)
            {
                _banMessage = newMessage;
            }
        });
    }

    private void ReloadSubnetList()
    {
        var list = _config.GetValue<List<object>>("SubnetBanList", new List<object>());
        _subnetList.Clear();

        if (list != null && list.Count > 0)
        {
            foreach (var element in list)
            {
                var ban = element?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(ban))
                {
                    _subnetList.Add(ban);
                }
            }
            _logger.LogInformation("Loaded {Count} banned subnets", _subnetList.Count);
        }
        else
        {
            // Set empty list as default (only if it doesn't exist)
            _ = _config.SetValueAsync("SubnetBanList", new List<string>());
        }
    }

    private void ReloadBanMessage()
    {
        _banMessage = _config.GetValue<string>("BanMessage", "You are not allowed to join this server.");
        _ = _config.SetValueAsync("BanMessage", _banMessage);
    }

    private void RegisterInteraction()
    {
        // Unregister existing interaction first
        _interactionRegistration.UnregisterInteraction(SubnetBanlistKey);

        // Register new interaction
        _interactionRegistration.RegisterInteraction(SubnetBanlistKey, async (clientId, game, token) =>
        {
            var interaction = new InteractionData
            {
                Name = "Subnet Banlist",
                Description = $"List of banned subnets ({_subnetList.Count} Total)",
                DisplayMeta = "ph-x-circle",
                InteractionId = SubnetBanlistKey,
                MinimumPermission = EFClient.Permission.Moderator,
                InteractionType = InteractionType.TemplateContent,
                Source = Name
            };

            interaction.Action = async (sourceId, targetId, g, meta, ct) =>
            {
                var table = "<table class=\"w-full text-left border-collapse\">";

                var unbanSubnetInteraction = new Dictionary<string, string>
                {
                    { "InteractionId", "command" },
                    { "Data", "unbansubnet" },
                    { "ActionButtonLabel", "Unban" },
                    { "Name", "Unban Subnet" }
                };

                if (_subnetList.Count == 0)
                {
                    table += "<tr><td colspan=\"2\" class=\"px-6 py-8 text-center text-muted\">No subnets are banned.</td></tr>";
                }
                else
                {
                    foreach (var subnet in _subnetList)
                    {
                        unbanSubnetInteraction["Data"] = "unbansubnet " + subnet;
                        var encodedMeta = Uri.EscapeDataString(System.Text.Json.JsonSerializer.Serialize(unbanSubnetInteraction));
                        
                        table += $@"<tr class=""border-t border-line hover:bg-surface-hover/30 transition-colors"">
                                    <td class=""px-6 py-4 whitespace-nowrap"">
                                        <span class=""font-mono text-sm text-foreground"">{subnet}</span>
                                    </td>
                                    <td class=""px-6 py-4 text-right"">
                                        <button type=""button"" class=""profile-action cursor-pointer"" data-action=""DynamicAction""
                                           data-action-meta=""{encodedMeta}"">
                                            <div class=""inline-flex items-center px-3 py-1.5 rounded-lg bg-red-600/20 text-red-400 border border-red-500/30 hover:bg-red-600/30 transition-colors text-sm font-medium"">
                                                <i class=""ph ph-x-circle mr-2 text-sm""></i>
                                                <span class=""truncate"">Unban Subnet</span>
                                            </div>
                                        </button>
                                    </td>
                                </tr>";
                    }
                }

                table += "</table>";
                return table;
            };

            return interaction;
        });
    }

    private Task OnClientAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        if (!IsSubnetBanned(clientEvent.Client.IPAddressString, _subnetList))
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Kicking {Client} because they are subnet banned.", clientEvent.Client);
        clientEvent.Client.Kick(_banMessage, clientEvent.Client.CurrentServer.AsConsoleClient());
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.ClientStateAuthorized -= OnClientAuthorized;
        _interactionRegistration.UnregisterInteraction(SubnetBanlistKey);
        _logger.LogInformation("Subnet Ban unloaded");
    }

    private static bool ValidCidr(string input)
    {
        return !string.IsNullOrWhiteSpace(input) && CidrRegex.IsMatch(input);
    }

    private static long ConvertIpToLong(string ip)
    {
        if (IPAddress.TryParse(ip, out var ipAddress))
        {
            var bytes = ipAddress.GetAddressBytes();
            return ((long)bytes[0] << 24) + (bytes[1] << 16) + (bytes[2] << 8) + bytes[3];
        }
        return -1;
    }

    private static bool IsInSubnet(string ip, string subnet)
    {
        var parts = subnet.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        var baseIp = ConvertIpToLong(parts[0]);
        var longIp = ConvertIpToLong(ip);

        if (baseIp < 0 || longIp < 0)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
        {
            return false;
        }

        var freedom = (long)Math.Pow(2, 32 - prefixLength);
        return longIp >= baseIp && longIp < baseIp + freedom;
    }

    private static bool IsSubnetBanned(string ip, IReadOnlyList<string> list)
    {
        return list.Any(subnet => IsInSubnet(ip, subnet));
    }
}

/// <summary>
/// Command to ban an IPv4 subnet in CIDR notation.
/// Usage: !bansubnet 192.168.1.0/24
/// </summary>
public class BanSubnetCommand : Command
{
    private readonly ICsScriptPluginConfiguration _config;

    public BanSubnetCommand(CommandConfiguration config, ITranslationLookup translationLookup, ICsScriptPluginConfiguration scriptConfig)
        : base(config, translationLookup)
    {
        Name = "bansubnet";
        Description = "bans an IPv4 subnet";
        Alias = "bs";
        Permission = EFClient.Permission.SeniorAdmin;
        RequiresTarget = false;
        _config = scriptConfig;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var input = gameEvent.Data?.Trim() ?? string.Empty;

        if (!IsValidCidr(input))
        {
            gameEvent.Origin.Tell("Invalid CIDR input");
            return;
        }

        // Get current list
        var list = _config.GetValue<List<object>>("SubnetBanList", new List<object>());
        var subnetList = list?.Select(item => item?.ToString() ?? string.Empty)
            .Where(item => !string.IsNullOrEmpty(item))
            .ToList() ?? new List<string>();

        // Check if already banned
        if (subnetList.Contains(input))
        {
            gameEvent.Origin.Tell($"Subnet {input} is already banned");
            return;
        }

        // Add to list
        subnetList.Add(input);
        await _config.SetValueAsync("SubnetBanList", subnetList);

        gameEvent.Origin.Tell($"Added {input} to subnet banlist");
    }

    private static bool IsValidCidr(string input)
    {
        return !string.IsNullOrWhiteSpace(input) && 
               System.Text.RegularExpressions.Regex.IsMatch(input, @"^([0-9]{1,3}\.){3}[0-9]{1,3}(\/([0-9]|[1-2][0-9]|3[0-2]))?$");
    }
}

/// <summary>
/// Command to unban an IPv4 subnet in CIDR notation.
/// Usage: !unbansubnet 192.168.1.0/24
/// </summary>
public class UnbanSubnetCommand : Command
{
    private readonly ICsScriptPluginConfiguration _config;

    public UnbanSubnetCommand(CommandConfiguration config, ITranslationLookup translationLookup, ICsScriptPluginConfiguration scriptConfig)
        : base(config, translationLookup)
    {
        Name = "unbansubnet";
        Description = "unbans an IPv4 subnet";
        Alias = "ubs";
        Permission = EFClient.Permission.SeniorAdmin;
        RequiresTarget = false;
        _config = scriptConfig;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var input = gameEvent.Data?.Trim() ?? string.Empty;

        if (!IsValidCidr(input))
        {
            gameEvent.Origin.Tell("Invalid CIDR input");
            return;
        }

        // Get current list
        var list = _config.GetValue<List<object>>("SubnetBanList", new List<object>());
        var subnetList = list?.Select(item => item?.ToString() ?? string.Empty)
            .Where(item => !string.IsNullOrEmpty(item))
            .ToList() ?? new List<string>();

        if (!subnetList.Contains(input))
        {
            gameEvent.Origin.Tell("Subnet is not banned");
            return;
        }

        // Remove from list
        subnetList.Remove(input);
        await _config.SetValueAsync("SubnetBanList", subnetList);

        gameEvent.Origin.Tell($"Removed {input} from subnet banlist");
    }

    private static bool IsValidCidr(string input)
    {
        return !string.IsNullOrWhiteSpace(input) && 
               System.Text.RegularExpressions.Regex.IsMatch(input, @"^([0-9]{1,3}\.){3}[0-9]{1,3}(\/([0-9]|[1-2][0-9]|3[0-2]))?$");
    }
}
