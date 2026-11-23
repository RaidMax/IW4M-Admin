using System;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using Humanizer;
using Data.Abstractions;
using Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces.Events;
using static Data.Models.Client.EFClient;

namespace IW4MAdmin.Plugins.Welcome;

public class Plugin : IPluginV2
{
    public string Author => "RaidMax";
    public string Version => "1.1";
    public string Name => "Welcome Plugin";

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<WelcomeConfiguration>("WelcomePluginSettings");
    }

    private readonly WelcomeConfiguration _configuration;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IGeoLocationService _geoLocationService;

    public Plugin(WelcomeConfiguration configuration, IDatabaseContextFactory contextFactory,
        IGeoLocationService geoLocationService)
    {
        _configuration = configuration;
        _contextFactory = contextFactory;
        _geoLocationService = geoLocationService;

        IManagementEventSubscriptions.ClientStateAuthorized += OnClientStateAuthorized;
    }

    private async Task OnClientStateAuthorized(ClientStateEvent clientState, CancellationToken token)
    {
        var newPlayer = clientState.Client;

        if (newPlayer.Level >= Permission.Trusted && !newPlayer.Masked ||
            !string.IsNullOrEmpty(newPlayer.Tag) &&
            newPlayer.Level != Permission.Flagged && newPlayer.Level != Permission.Banned &&
            !newPlayer.Masked)
            newPlayer.CurrentServer.Broadcast(
                await ProcessAnnouncement(_configuration.PrivilegedAnnouncementMessage,
                    newPlayer));

        newPlayer.Tell(await ProcessAnnouncement(_configuration.UserWelcomeMessage, newPlayer));

        if (newPlayer.Level == Permission.Flagged)
        {
            string penaltyReason;

            await using var context = _contextFactory.CreateContext(false);
            {
                penaltyReason = await context.Penalties
                    .Where(p => p.OffenderId == newPlayer.ClientId && p.Type == EFPenalty.PenaltyType.Flag)
                    .OrderByDescending(p => p.When)
                    .Select(p => p.AutomatedOffense ?? p.Offense)
                    .FirstOrDefaultAsync(cancellationToken: token);
            }

            newPlayer.CurrentServer.ToAdmins(Utilities.CurrentLocalization
                .LocalizationIndex["PLUGINS_WELCOME_FLAG_MESSAGE"]
                .FormatExt(newPlayer.Name, penaltyReason));
        }
        else
        {
            newPlayer.CurrentServer.Broadcast(await ProcessAnnouncement(_configuration.UserAnnouncementMessage,
                newPlayer));
        }
    }

    private async Task<string> ProcessAnnouncement(string msg, EFClient joining)
    {
        msg = msg.Replace("{{ClientName}}", joining.Name);
        msg = msg.Replace("{{ClientLevel}}",
            $"{Utilities.ConvertLevelToColor(joining.Level, joining.ClientPermission.Name)}{(string.IsNullOrEmpty(joining.Tag) ? "" : $" (Color::White){joining.Tag}(Color::White)")}");
        // this prevents it from trying to evaluate it every message
        if (msg.Contains("{{ClientLocation}}"))
        {
            msg = msg.Replace("{{ClientLocation}}", await GetCountryName(joining.IPAddressString));
        }

        msg = msg.Replace("{{TimesConnected}}",
            joining.Connections.Ordinalize(Utilities.CurrentLocalization.Culture));

        return msg;
    }

    /// <summary>
    /// looks up the IP origin using the local geolocation database
    /// </summary>
    /// <param name="ip">IP address to get location of</param>
    /// <returns></returns>
    private async Task<string> GetCountryName(string ip)
    {
        try
        {
            var result = await _geoLocationService.Locate(ip);

            return string.IsNullOrEmpty(result?.CountryCode)
                ? Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_WELCOME_UNKNOWN_COUNTRY"]
                : result.Country;
        }
        catch
        {
            return Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_WELCOME_UNKNOWN_IP"];
        }
    }
}
