#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.10.1

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Broadcasts ban messages to all servers when a client is banned.
/// Uses the shared ScriptPluginSettings.json configuration file for simple settings.
/// </summary>
public class BanBroadcastingPlugin : IPluginV2
{
    public string Name => "Broadcast Bans";
    public string Author => "Amos, RaidMax";
    public string Version => "2.1";

    private readonly ILogger<BanBroadcastingPlugin> _logger;
    private readonly ICsScriptPluginConfiguration _config;
    private readonly ITranslationLookup _translationLookup;
    private readonly IManager _manager;

    public BanBroadcastingPlugin(
        ILogger<BanBroadcastingPlugin> logger,
        ICsScriptPluginConfiguration config,
        ITranslationLookup translationLookup,
        IManager manager)
    {
        _logger = logger;
        _config = config; // Shared ScriptPluginSettings.json configuration
        _translationLookup = translationLookup;
        _manager = manager;

        // Subscribe to penalty events
        IManagementEventSubscriptions.ClientPenaltyAdministered += OnClientPenalty;

        // Load configuration with default
        var enabled = _config.GetValue<bool>("EnableBroadcastBans", false);
        
        // Queue write of default value (will only be written if key doesn't exist after plugin name is set)
        // This ensures defaults are saved on first load without overwriting user settings on reload
        _ = _config.SetValueAsync("EnableBroadcastBans", enabled);

        _logger.LogInformation("{Name} {Version} by {Author} loaded. Enabled={Enabled}",
            Name, Version, Author, enabled);
    }

    private Task OnClientPenalty(ClientPenaltyEvent penaltyEvent, CancellationToken token)
    {
        // Check if broadcasting is enabled (supports hot-reload from ScriptPluginSettings.json)
        var enabled = _config.GetValue<bool>("EnableBroadcastBans", false);
        
        if (!enabled || penaltyEvent.Penalty.Type != Data.Models.EFPenalty.PenaltyType.Ban)
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
