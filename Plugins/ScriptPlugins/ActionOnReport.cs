#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Models.Client;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Action on Report Plugin - Automatically bans or temporarily bans players
/// after they receive a certain number of reports.
/// </summary>
public class ActionOnReportPlugin : IPluginV2
{
    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<ActionOnReportConfig>(
            "ActionOnReportSettings",
            new ActionOnReportConfig());
    }

    public string Name => "Action on Report";
    public string Author => "RaidMax";
    public string Version => "2.1";

    private readonly ILogger<ActionOnReportPlugin> _logger;
    private readonly ActionOnReportConfig _config;
    private readonly ITranslationLookup _translationLookup;
    private readonly Dictionary<long, int> _reportCounts = new();

    public ActionOnReportPlugin(
        ILogger<ActionOnReportPlugin> logger,
        ActionOnReportConfig config,
        ITranslationLookup translationLookup)
    {
        _logger = logger;
        _config = config;
        _translationLookup = translationLookup;

        // Subscribe to penalty events
        IManagementEventSubscriptions.ClientPenaltyAdministered += OnPenalty;

        _logger.LogInformation("ActionOnReport {Version} by {Author} loaded. Enabled={Enabled}", 
            Version, Author, _config.Enabled);
    }


    private Task OnPenalty(ClientPenaltyEvent penaltyEvent, CancellationToken token)
    {
        if (!_config.Enabled || penaltyEvent.Penalty.Type != Data.Models.EFPenalty.PenaltyType.Report)
        {
            return Task.CompletedTask;
        }

        var client = penaltyEvent.Client;

        // Ignore if client is not in-game or is privileged
        if (!client.IsIngame || (client.Level != EFClient.Permission.User && client.Level != EFClient.Permission.Flagged))
        {
            _logger.LogInformation("Ignoring report for client (id) {ClientId} because they are privileged or not in-game", 
                client.ClientId);
            return Task.CompletedTask;
        }

        // Get and increment report count
        if (!_reportCounts.TryGetValue(client.NetworkId, out var reportCount))
        {
            reportCount = 0;
        }
        reportCount++;
        _reportCounts[client.NetworkId] = reportCount;

        if (reportCount >= _config.MaxReportCount)
        {
            var reason = _translationLookup["PLUGINS_REPORT_ACTION"] ?? "Too many reports";

            switch (_config.ReportAction)
            {
                case "TempBan":
                    _logger.LogInformation("TempBanning client (id) {ClientId} because they received {ReportCount} reports", 
                        client.ClientId, reportCount);
                    client.TempBan(reason, TimeSpan.FromMinutes(_config.TempBanDurationMinutes), 
                        client.CurrentServer.AsConsoleClient());
                    break;

                case "Ban":
                    _logger.LogInformation("Banning client (id) {ClientId} because they received {ReportCount} reports", 
                        client.ClientId, reportCount);
                    client.Ban(reason, client.CurrentServer.AsConsoleClient(), false);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.ClientPenaltyAdministered -= OnPenalty;
        _logger.LogInformation("ActionOnReport unloaded");
    }
}

/// <summary>
/// Configuration class for ActionOnReport plugin.
/// </summary>
public class ActionOnReportConfig
{
    /// <summary>
    /// Indicates if the plugin is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Action to take when report threshold is reached. Can be "TempBan" or "Ban".
    /// </summary>
    public string ReportAction { get; set; } = "TempBan";

    /// <summary>
    /// How many reports before action is taken.
    /// </summary>
    public int MaxReportCount { get; set; } = 5;

    /// <summary>
    /// How long to temporarily ban the player (in minutes).
    /// </summary>
    public int TempBanDurationMinutes { get; set; } = 60;
}
