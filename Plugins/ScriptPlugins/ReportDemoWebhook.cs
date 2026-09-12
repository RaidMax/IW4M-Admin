#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Report Demo Webhook - posts player reports and vote bans to a Discord webhook together with
/// the server demo that was recording at the time, so admins can review the footage.
///
/// Flow:
///  1. A report (or a vote ban) is administered -> an embed is posted immediately with the details and
///     the name of the demo currently being recorded on that server.
///  2. When that match ends (or after a timeout), the demo is copied into <see cref="ReportDemoWebhookConfig.KeepDirectory"/>
///     so the nightly demo cleanup cannot delete it, and a follow-up embed is posted with a download link
///     (and the file attached when it is small enough for Discord).
///  3. Vote bans are treated as highlighted VODs: red embed, VOTEBAN_ prefix on the kept copy.
/// </summary>
public class ReportDemoWebhookPlugin : IPluginV2
{
    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<ReportDemoWebhookConfig>("ReportDemoWebhookSettings", new ReportDemoWebhookConfig());
    }

    public string Name => "Report Demo Webhook";
    public string Author => "CUKServers";
    public string Version => "1.0";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int ColourReport = 0xF59E0B;   // amber
    private const int ColourVoteBan = 0xEF4444;  // red
    private const int ColourDemo = 0x3B87F7;     // blue
    private const int ColourHighlight = 0xDC2626;

    private readonly ILogger<ReportDemoWebhookPlugin> _logger;
    private readonly ReportDemoWebhookConfig _config;
    private readonly ApplicationConfiguration _appConfig;

    private readonly object _lock = new();
    private readonly Dictionary<string, PendingDemo> _pending = new(); // keyed by demo path (or server key when no demo)
    private readonly CancellationTokenSource _cts = new();

    public ReportDemoWebhookPlugin(ILogger<ReportDemoWebhookPlugin> logger, ReportDemoWebhookConfig config,
        ApplicationConfiguration appConfig)
    {
        _logger = logger;
        _config = config;
        _appConfig = appConfig;

        IManagementEventSubscriptions.ClientPenaltyAdministered += OnPenalty;
        IGameEventSubscriptions.MatchEnded += OnMatchEnded;
        IManagementEventSubscriptions.Unload += OnUnload;

        _ = Task.Run(() => SweepLoopAsync(_cts.Token));

        _logger.LogInformation("ReportDemoWebhook {Version} loaded. Enabled={Enabled}, Servers with demos={Count}",
            Version, _config.Enabled, _config.Servers.Count);
    }

    #region Event handlers

    private Task OnPenalty(ClientPenaltyEvent penaltyEvent, CancellationToken token)
    {
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.WebhookUrl))
        {
            return Task.CompletedTask;
        }

        var penalty = penaltyEvent.Penalty;
        var client = penaltyEvent.Client;

        var isReport = penalty.Type == EFPenalty.PenaltyType.Report;
        var isVoteBan = !isReport && IsVoteBan(penalty);

        if (!isReport && !isVoteBan)
        {
            return Task.CompletedTask;
        }

        // Fire and forget so the penalty pipeline is never blocked by Discord.
        _ = Task.Run(() => HandleIncidentAsync(client, penalty, isVoteBan), _cts.Token);
        return Task.CompletedTask;
    }

    private Task OnMatchEnded(MatchEndEvent matchEndEvent, CancellationToken token)
    {
        if (!_config.Enabled)
        {
            return Task.CompletedTask;
        }

        var serverKey = ServerKey(matchEndEvent.Server?.ListenAddress, matchEndEvent.Server?.ListenPort ?? 0);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _config.MatchEndSettleSeconds)), _cts.Token);
                await ProcessPendingAsync(item => item.ServerKey == serverKey, "match ended");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReportDemoWebhook: failed processing demos after match end on {Server}", serverKey);
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    private Task OnUnload(IManager manager, CancellationToken token)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IManagementEventSubscriptions.ClientPenaltyAdministered -= OnPenalty;
        IGameEventSubscriptions.MatchEnded -= OnMatchEnded;
        IManagementEventSubscriptions.Unload -= OnUnload;
        _cts.Cancel();
        _logger.LogInformation("ReportDemoWebhook unloaded");
    }

    #endregion

    #region Incident handling

    private async Task HandleIncidentAsync(EFClient client, EFPenalty penalty, bool isVoteBan)
    {
        try
        {
            var server = client.CurrentServer;
            var serverKey = server is null ? "unknown" : ServerKey(server.ListenAddress, server.ListenPort);
            var serverName = server?.ServerName?.StripColors() ?? "Unknown server";
            var map = server?.CurrentMap?.Alias ?? server?.CurrentMap?.Name ?? "unknown";
            var gametype = server?.Gametype ?? "";
            var demoPath = FindCurrentDemo(serverKey, out var demoDirectory);
            var playersOnline = server?.GetClientsAsList()
                .Select(c => c.CleanedName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Take(_config.MaxPlayersListed)
                .ToList() ?? new List<string>();

            var incident = new Incident
            {
                IsVoteBan = isVoteBan,
                OffenderName = client.CleanedName,
                OffenderId = client.ClientId,
                PunisherName = penalty.Punisher?.CleanedName ?? "Unknown",
                PunisherId = penalty.Punisher?.ClientId ?? 0,
                Reason = string.IsNullOrWhiteSpace(penalty.Offense) ? "No reason given" : penalty.Offense.StripColors(),
                When = DateTime.UtcNow,
                ServerKey = serverKey,
                ServerName = serverName,
                Map = map,
                Gametype = gametype,
                DemoPath = demoPath,
                Expires = penalty.Expires
            };

            var embed = BuildIncidentEmbed(incident, demoDirectory is not null, playersOnline);
            await PostAsync(new WebhookPayload { Username = _config.WebhookUsername, Embeds = new[] { embed } }, null);

            if (demoDirectory is null)
            {
                return; // no demo recording for this server, nothing more to do
            }

            var pendingKey = demoPath ?? $"{serverKey}|nodemo";
            lock (_lock)
            {
                if (!_pending.TryGetValue(pendingKey, out var pending))
                {
                    pending = new PendingDemo
                    {
                        ServerKey = serverKey,
                        ServerName = serverName,
                        Map = map,
                        Gametype = gametype,
                        DemoPath = demoPath,
                        CreatedUtc = DateTime.UtcNow
                    };
                    _pending[pendingKey] = pending;
                }

                pending.Highlight |= isVoteBan;
                pending.Incidents.Add(incident);
            }

            _logger.LogInformation("ReportDemoWebhook: queued {Kind} for {Client} on {Server} (demo: {Demo})",
                isVoteBan ? "vote ban" : "report", client.CleanedName, serverName, demoPath ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReportDemoWebhook: failed to handle incident for client {ClientId}", client.ClientId);
        }
    }

    private bool IsVoteBan(EFPenalty penalty)
    {
        if (penalty.Type != EFPenalty.PenaltyType.TempBan && penalty.Type != EFPenalty.PenaltyType.Ban)
        {
            return false;
        }

        var text = $"{penalty.Offense} {penalty.AutomatedOffense}".ToLowerInvariant();
        return _config.VoteBanKeywords.Any(keyword => !string.IsNullOrWhiteSpace(keyword) && text.Contains(keyword.ToLowerInvariant()));
    }

    #endregion

    #region Pending demos

    private async Task SweepLoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            while (await timer.WaitForNextTickAsync(token))
            {
                if (!_config.Enabled)
                {
                    continue;
                }

                var timeout = TimeSpan.FromMinutes(Math.Max(5, _config.PendingTimeoutMinutes));
                await ProcessPendingAsync(item =>
                {
                    if (DateTime.UtcNow - item.CreatedUtc > timeout)
                    {
                        return true;
                    }

                    // The match ended without an event: the demo stopped growing and a newer one exists.
                    if (item.DemoPath is null || !File.Exists(item.DemoPath))
                    {
                        return DateTime.UtcNow - item.CreatedUtc > TimeSpan.FromMinutes(5);
                    }

                    var lastWrite = File.GetLastWriteTimeUtc(item.DemoPath);
                    var quietFor = DateTime.UtcNow - lastWrite;
                    var newest = NewestDemo(Path.GetDirectoryName(item.DemoPath)!);
                    return quietFor > TimeSpan.FromMinutes(3) && newest is not null && !PathEquals(newest, item.DemoPath);
                }, "timeout / demo finished");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReportDemoWebhook: sweep loop failed");
        }
    }

    private async Task ProcessPendingAsync(Func<PendingDemo, bool> selector, string trigger)
    {
        List<KeyValuePair<string, PendingDemo>> ready;
        lock (_lock)
        {
            ready = _pending.Where(kv => selector(kv.Value)).ToList();
            foreach (var kv in ready)
            {
                _pending.Remove(kv.Key);
            }
        }

        foreach (var (_, pending) in ready)
        {
            try
            {
                await FinaliseDemoAsync(pending, trigger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReportDemoWebhook: failed to finalise demo {Demo}", pending.DemoPath);
            }
        }
    }

    private async Task FinaliseDemoAsync(PendingDemo pending, string trigger)
    {
        string? keptPath = null;
        string? publicUrl = null;
        long size = 0;

        if (pending.DemoPath is not null && File.Exists(pending.DemoPath))
        {
            keptPath = CopyToKeep(pending);
            if (keptPath is not null)
            {
                size = new FileInfo(keptPath).Length;
                if (!string.IsNullOrWhiteSpace(_config.PublicBaseUrl))
                {
                    publicUrl = _config.PublicBaseUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(Path.GetFileName(keptPath));
                }
            }
        }

        var embed = BuildDemoEmbed(pending, keptPath, publicUrl, size, trigger);
        var attach = keptPath is not null && size > 0 && size <= _config.MaxAttachmentBytes ? keptPath : null;
        await PostAsync(new WebhookPayload { Username = _config.WebhookUsername, Embeds = new[] { embed } }, attach);

        _logger.LogInformation("ReportDemoWebhook: finalised {Count} incident(s) on {Server}; demo {Demo} kept as {Kept} ({Trigger})",
            pending.Incidents.Count, pending.ServerName, pending.DemoPath ?? "none", keptPath ?? "not copied", trigger);
    }

    private string? CopyToKeep(PendingDemo pending)
    {
        try
        {
            Directory.CreateDirectory(_config.KeepDirectory);

            var original = Path.GetFileName(pending.DemoPath!);
            var serverSlug = Slug(Path.GetFileName(Path.GetDirectoryName(pending.DemoPath!)!.TrimEnd('/', '\\')));
            var kind = pending.Highlight ? "VOTEBAN" : "REPORT";
            var name = $"{pending.CreatedUtc:yyyyMMdd-HHmm}_{serverSlug}_{kind}_{original}";
            var destination = Path.Combine(_config.KeepDirectory, name);

            File.Copy(pending.DemoPath!, destination, true);

            // Plutonium writes a .json sidecar with map/gametype/length; keep it next to the demo.
            var sidecar = Path.ChangeExtension(pending.DemoPath!, ".json");
            if (File.Exists(sidecar))
            {
                File.Copy(sidecar, Path.ChangeExtension(destination, ".json"), true);
            }

            return destination;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReportDemoWebhook: could not copy demo {Demo} to {Keep}", pending.DemoPath, _config.KeepDirectory);
            return null;
        }
    }

    #endregion

    #region Demo discovery

    private string? FindCurrentDemo(string serverKey, out string? demoDirectory)
    {
        demoDirectory = null;
        if (!_config.Servers.TryGetValue(serverKey, out var serverConfig) || string.IsNullOrWhiteSpace(serverConfig.DemoDirectory))
        {
            return null;
        }

        demoDirectory = serverConfig.DemoDirectory;
        if (!Directory.Exists(demoDirectory))
        {
            _logger.LogWarning("ReportDemoWebhook: demo directory {Dir} for {Server} does not exist", demoDirectory, serverKey);
            return null;
        }

        return NewestDemo(demoDirectory);
    }

    private string? NewestDemo(string directory)
    {
        try
        {
            return new DirectoryInfo(directory)
                .EnumerateFiles()
                .Where(f => _config.DemoExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReportDemoWebhook: could not enumerate {Dir}", directory);
            return null;
        }
    }

    #endregion

    #region Discord

    private DiscordEmbed BuildIncidentEmbed(Incident incident, bool serverRecordsDemos, List<string> playersOnline)
    {
        var fields = new List<DiscordField>
        {
            new(incident.IsVoteBan ? "Banned player" : "Reported player", ProfileLink(incident.OffenderName, incident.OffenderId), true),
            new(incident.IsVoteBan ? "Started by" : "Reported by", ProfileLink(incident.PunisherName, incident.PunisherId), true),
            new("Reason", Truncate(incident.Reason, 900), false),
            new("Server", incident.ServerName, true),
            new("Map / mode", string.IsNullOrEmpty(incident.Gametype) ? incident.Map : $"{incident.Map} · {incident.Gametype}", true)
        };

        if (incident.IsVoteBan && incident.Expires.HasValue)
        {
            fields.Add(new DiscordField("Expires", $"<t:{new DateTimeOffset(incident.Expires.Value).ToUnixTimeSeconds()}:R>", true));
        }

        if (playersOnline.Count > 0)
        {
            fields.Add(new DiscordField($"Players online ({playersOnline.Count})", Truncate(string.Join(", ", playersOnline), 1000), false));
        }

        string demoText;
        if (!serverRecordsDemos)
        {
            demoText = "This server does not record demos.";
        }
        else if (incident.DemoPath is null)
        {
            demoText = "No demo is being recorded right now.";
        }
        else
        {
            demoText = $"`{Path.GetFileName(incident.DemoPath)}` — recording now, will be posted here when the match ends.";
        }
        fields.Add(new DiscordField("Demo", demoText, false));

        return new DiscordEmbed
        {
            Title = incident.IsVoteBan ? $"🔴 Vote ban: {incident.OffenderName}" : $"🚩 Report: {incident.OffenderName}",
            Color = incident.IsVoteBan ? ColourVoteBan : ColourReport,
            Fields = fields,
            Timestamp = incident.When.ToString("o"),
            Footer = new DiscordFooter($"{_config.FooterText} · {incident.ServerKey}")
        };
    }

    private DiscordEmbed BuildDemoEmbed(PendingDemo pending, string? keptPath, string? publicUrl, long size, string trigger)
    {
        var fields = new List<DiscordField>();

        foreach (var incident in pending.Incidents.Take(10))
        {
            var label = incident.IsVoteBan ? "🔴 Vote ban" : "🚩 Report";
            fields.Add(new DiscordField(
                $"{label} · {incident.When:HH:mm} UTC",
                $"{ProfileLink(incident.OffenderName, incident.OffenderId)} by {ProfileLink(incident.PunisherName, incident.PunisherId)}\n{Truncate(incident.Reason, 300)}",
                false));
        }

        if (pending.Incidents.Count > 10)
        {
            fields.Add(new DiscordField("More", $"+{pending.Incidents.Count - 10} further incident(s) on this demo", false));
        }

        fields.Add(new DiscordField("Server", pending.ServerName, true));
        fields.Add(new DiscordField("Map / mode", string.IsNullOrEmpty(pending.Gametype) ? pending.Map : $"{pending.Map} · {pending.Gametype}", true));

        string demoText;
        if (keptPath is null)
        {
            demoText = pending.DemoPath is null ? "No demo was recorded for this match." : "The demo could not be copied; check the IW4MAdmin log.";
        }
        else
        {
            var sizeMb = size / 1024.0 / 1024.0;
            demoText = publicUrl is not null
                ? $"[{Path.GetFileName(keptPath)}]({publicUrl}) ({sizeMb:0.0} MB)"
                : $"`{keptPath}` ({sizeMb:0.0} MB)";
            if (size > _config.MaxAttachmentBytes)
            {
                demoText += " — too large to attach, use the link.";
            }
        }
        fields.Add(new DiscordField(pending.Highlight ? "📼 Highlighted VOD" : "📼 Demo", demoText, false));

        var count = pending.Incidents.Count;
        return new DiscordEmbed
        {
            Title = pending.Highlight
                ? $"📼 VOD highlighted — vote ban on {pending.ServerName}"
                : $"📼 Demo ready — {count} report{(count == 1 ? "" : "s")} on {pending.ServerName}",
            Color = pending.Highlight ? ColourHighlight : ColourDemo,
            Fields = fields,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Footer = new DiscordFooter($"{_config.FooterText} · {trigger}")
        };
    }

    private async Task PostAsync(WebhookPayload payload, string? attachmentPath)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _config.WebhookUrl);

                if (attachmentPath is not null)
                {
                    var form = new MultipartFormDataContent();
                    form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "payload_json");
                    var bytes = await File.ReadAllBytesAsync(attachmentPath);
                    form.Add(new ByteArrayContent(bytes), "files[0]", Path.GetFileName(attachmentPath));
                    request.Content = form;
                }
                else
                {
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using var response = await Http.SendAsync(request, _cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                if ((int)response.StatusCode == 413 && attachmentPath is not null)
                {
                    _logger.LogWarning("ReportDemoWebhook: Discord rejected the attachment as too large, resending without it");
                    attachmentPath = null;
                    continue;
                }

                if ((int)response.StatusCode == 429)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                    await Task.Delay(retryAfter, _cts.Token);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("ReportDemoWebhook: webhook returned {Status}: {Body}", (int)response.StatusCode, Truncate(body, 300));
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ReportDemoWebhook: webhook post failed (attempt {Attempt})", attempt);
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt), _cts.Token);
            }
        }
    }

    private string ProfileLink(string name, int clientId)
    {
        var safeName = Truncate(name.Replace("]", "\\]"), 60);
        var baseUrl = (_config.PublicWebfrontUrl ?? _appConfig.WebfrontUrl ?? string.Empty).TrimEnd('/');
        return clientId > 0 && !string.IsNullOrWhiteSpace(baseUrl)
            ? $"[{safeName}]({baseUrl}/client/{clientId})"
            : safeName;
    }

    #endregion

    #region Helpers

    private static string ServerKey(string? address, int port) => $"{address ?? "0.0.0.0"}:{port}";

    private static bool PathEquals(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static string Slug(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..(max - 1)] + "…";

    #endregion

    #region Types

    private sealed class Incident
    {
        public bool IsVoteBan { get; init; }
        public string OffenderName { get; init; } = "";
        public int OffenderId { get; init; }
        public string PunisherName { get; init; } = "";
        public int PunisherId { get; init; }
        public string Reason { get; init; } = "";
        public DateTime When { get; init; }
        public string ServerKey { get; init; } = "";
        public string ServerName { get; init; } = "";
        public string Map { get; init; } = "";
        public string Gametype { get; init; } = "";
        public string? DemoPath { get; init; }
        public DateTime? Expires { get; init; }
    }

    private sealed class PendingDemo
    {
        public string ServerKey { get; init; } = "";
        public string ServerName { get; init; } = "";
        public string Map { get; init; } = "";
        public string Gametype { get; init; } = "";
        public string? DemoPath { get; init; }
        public DateTime CreatedUtc { get; init; }
        public bool Highlight { get; set; }
        public List<Incident> Incidents { get; } = new();
    }

    private sealed class WebhookPayload
    {
        public string? Username { get; set; }
        public DiscordEmbed[] Embeds { get; set; } = Array.Empty<DiscordEmbed>();
    }

    private sealed class DiscordEmbed
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int Color { get; set; }
        public List<DiscordField> Fields { get; set; } = new();
        public string? Timestamp { get; set; }
        public DiscordFooter? Footer { get; set; }
    }

    private sealed record DiscordField(string Name, string Value, bool Inline);

    private sealed record DiscordFooter(string Text);

    #endregion
}

/// <summary>
/// Configuration for the Report Demo Webhook plugin (Configuration/ReportDemoWebhookSettings.json).
/// </summary>
public class ReportDemoWebhookConfig
{
    /// <summary>Master switch.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Discord webhook URL that receives reports, vote bans and demos.</summary>
    public string WebhookUrl { get; set; } = "";

    /// <summary>Display name used for webhook posts.</summary>
    public string WebhookUsername { get; set; } = "Report Watch";

    /// <summary>Footer text on every embed.</summary>
    public string FooterText { get; set; } = "IW4MAdmin";

    /// <summary>Base URL used for player profile links, e.g. https://cukservers.net. Falls back to the configured webfront URL.</summary>
    public string? PublicWebfrontUrl { get; set; }

    /// <summary>Directory (inside the IW4MAdmin process) where reported demos are copied so cleanup jobs never delete them.</summary>
    public string KeepDirectory { get; set; } = "kept-demos";

    /// <summary>Public URL that serves <see cref="KeepDirectory"/>, e.g. https://cukservers.net/demos. Leave empty to post the path only.</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Largest demo that is attached to the Discord post directly (Discord webhooks accept about 10 MB).</summary>
    public long MaxAttachmentBytes { get; set; } = 8_000_000;

    /// <summary>File extensions that count as demos.</summary>
    public List<string> DemoExtensions { get; set; } = new() { ".demo", ".dm_13", ".dm_1" };

    /// <summary>Penalty reasons containing any of these words are treated as vote bans.</summary>
    public List<string> VoteBanKeywords { get; set; } = new() { "vote" };

    /// <summary>Seconds to wait after a match ends before copying the demo, so the game can finish writing it.</summary>
    public int MatchEndSettleSeconds { get; set; } = 20;

    /// <summary>Post the demo anyway after this many minutes if no match-end event arrives.</summary>
    public int PendingTimeoutMinutes { get; set; } = 60;

    /// <summary>How many online player names to include in the report embed.</summary>
    public int MaxPlayersListed { get; set; } = 18;

    /// <summary>Per-server settings keyed by "ip:port" as configured in IW4MAdminSettings.json.</summary>
    public Dictionary<string, ReportDemoServerConfig> Servers { get; set; } = new();
}

public class ReportDemoServerConfig
{
    /// <summary>Directory the game server writes its demos to (as seen by the IW4MAdmin process).</summary>
    public string DemoDirectory { get; set; } = "";
}
