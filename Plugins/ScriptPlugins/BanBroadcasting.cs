#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Broadcasts ban messages to all servers when a client is banned.
/// </summary>
public class BanBroadcastingPlugin : IPluginV2
{
    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<BanBroadcastingConfiguration>(
            "BanBroadcastingSettings",
            new BanBroadcastingConfiguration());
    }

    public string Name => "Broadcast Bans";
    public string Author => "Amos, RaidMax";
    public string Version => "2.1";

    private readonly ILogger<BanBroadcastingPlugin> _logger;
    private readonly BanBroadcastingConfiguration _config;
    private readonly ITranslationLookup _translationLookup;
    private readonly IManager _manager;

    public BanBroadcastingPlugin(
        ILogger<BanBroadcastingPlugin> logger,
        BanBroadcastingConfiguration config,
        ITranslationLookup translationLookup,
        IManager manager)
    {
        _logger = logger;
        _config = config;
        _translationLookup = translationLookup;
        _manager = manager;

        // Subscribe to penalty events
        IManagementEventSubscriptions.ClientPenaltyAdministered += OnClientPenalty;

        _logger.LogInformation("{Name} {Version} by {Author} loaded. Enabled={Enabled}",
            Name, Version, Author, _config.EnableBroadcastBans);
    }

    private Task OnClientPenalty(ClientPenaltyEvent penaltyEvent, CancellationToken token)
    {
        // Check if broadcasting is enabled
        if (!_config.EnableBroadcastBans || penaltyEvent.Penalty.Type != Data.Models.EFPenalty.PenaltyType.Ban)
        {
            return Task.CompletedTask;
        }

        string? automatedPenaltyMessage = null;

        // Check if the punisher has any automated offenses
        if (penaltyEvent.Penalty.Punisher?.AdministeredPenalties != null)
        {
            foreach (var penalty in penaltyEvent.Penalty.Punisher.AdministeredPenalties)
            {
                automatedPenaltyMessage = penalty.AutomatedOffense;
                break; // Just get the first one if any
            }
        }

        string message;
        
        // Check if the ban was automated (punisher is system client ID 1) and has an automated offense message
        if (penaltyEvent.Penalty.PunisherId == 1 && !string.IsNullOrEmpty(automatedPenaltyMessage))
        {
            var template = _translationLookup["PLUGINS_BROADCAST_BAN_ACMESSAGE"];
            message = template?.Replace("{{targetClient}}", penaltyEvent.Client.CleanedName) 
                ?? $"^1{penaltyEvent.Client.CleanedName} ^7has been banned by anti-cheat";
        }
        else
        {
            var template = _translationLookup["PLUGINS_BROADCAST_BAN_MESSAGE"];
            message = template?.Replace("{{targetClient}}", penaltyEvent.Client.CleanedName) 
                ?? $"^1{penaltyEvent.Client.CleanedName} ^7has been banned";
        }

        BroadcastMessage(message);
        
        return Task.CompletedTask;
    }

    private void BroadcastMessage(string message)
    {
        foreach (var server in _manager.GetServers())
        {
            server.Broadcast(message);
        }
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.ClientPenaltyAdministered -= OnClientPenalty;
        _logger.LogInformation("{Name} unloaded", Name);
    }
}

/// <summary>
/// Configuration class for BanBroadcasting plugin.
/// </summary>
public class BanBroadcastingConfiguration
{
    /// <summary>
    /// Indicates if the plugin is enabled.
    /// </summary>
    public bool EnableBroadcastBans { get; set; } = false;
}
