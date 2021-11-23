using System;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Newtonsoft.Json.Linq;
using Humanizer;
using Data.Abstractions;
using Data.Models;
using static Data.Models.Client.EFClient;

namespace IW4MAdmin.Plugins.Welcome
{
    public class Plugin : IPlugin
    {
        public string Author => "RaidMax";

        public float Version => 1.0f;

        public string Name => "Welcome Plugin";

        private readonly IConfigurationHandler<WelcomeConfiguration> _configHandler;
        private readonly IDatabaseContextFactory _contextFactory;

        public Plugin(IConfigurationHandlerFactory configurationHandlerFactory, IDatabaseContextFactory contextFactory)
        {
            _configHandler =
                configurationHandlerFactory.GetConfigurationHandler<WelcomeConfiguration>("WelcomePluginSettings");
            _contextFactory = contextFactory;
        }

        public async Task OnLoadAsync(IManager manager)
        {
            if (_configHandler.Configuration() == null)
            {
                _configHandler.Set((WelcomeConfiguration) new WelcomeConfiguration().Generate());
                await _configHandler.Save();
            }
        }

        public Task OnUnloadAsync() => Task.CompletedTask;

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public async Task OnEventAsync(GameEvent gameEvent, Server server)
        {
            if (gameEvent.Type == GameEvent.EventType.Join)
            {
                var newPlayer = gameEvent.Origin;
                if (newPlayer.Level >= Permission.Trusted && !gameEvent.Origin.Masked||
                    !string.IsNullOrEmpty(newPlayer.GetAdditionalProperty<string>("ClientTag")) &&
                     newPlayer.Level != Permission.Flagged && newPlayer.Level != Permission.Banned &&
                     !newPlayer.Masked)
                    gameEvent.Owner.Broadcast(
                        await ProcessAnnouncement(_configHandler.Configuration().PrivilegedAnnouncementMessage,
                            newPlayer));

                newPlayer.Tell(await ProcessAnnouncement(_configHandler.Configuration().UserWelcomeMessage, newPlayer));

                if (newPlayer.Level == Permission.Flagged)
                {
                    string penaltyReason;

                    await using var context = _contextFactory.CreateContext(false);
                    {
                        penaltyReason = await context.Penalties
                            .Where(p => p.OffenderId == newPlayer.ClientId && p.Type == EFPenalty.PenaltyType.Flag)
                            .OrderByDescending(p => p.When)
                            .Select(p => p.AutomatedOffense ?? p.Offense)
                            .FirstOrDefaultAsync();
                    }

                    gameEvent.Owner.ToAdmins(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_WELCOME_FLAG_MESSAGE"]
                        .FormatExt(newPlayer.Name, penaltyReason));
                }
                else
                    gameEvent.Owner.Broadcast(await ProcessAnnouncement(_configHandler.Configuration().UserAnnouncementMessage,
                        newPlayer));
            }
        }

        private async Task<string> ProcessAnnouncement(string msg, EFClient joining)
        {
            msg = msg.Replace("{{ClientName}}", joining.Name);
            msg = msg.Replace("{{ClientLevel}}",
                $"{Utilities.ConvertLevelToColor(joining.Level, joining.ClientPermission.Name)}{(string.IsNullOrEmpty(joining.GetAdditionalProperty<string>("ClientTag")) ? "" : $" (Color::White)({joining.GetAdditionalProperty<string>("ClientTag")}(Color::White))")}");
            // this prevents it from trying to evaluate it every message
            if (msg.Contains("{{ClientLocation}}"))
            {
                msg = msg.Replace("{{ClientLocation}}", await GetCountryName(joining.IPAddressString));
            }

            msg = msg.Replace("{{TimesConnected}}", joining.Connections.Ordinalize());

            return msg;
        }

        /// <summary>
        /// makes a webrequest to determine IP origin 
        /// </summary>
        /// <param name="ip">IP address to get location of</param>
        /// <returns></returns>
        private async Task<string> GetCountryName(string ip)
        {
            using var wc = new WebClient();
            try
            {
                var response =
                    await wc.DownloadStringTaskAsync(new Uri($"http://extreme-ip-lookup.com/json/{ip}?key=demo"));
                var responseObj = JObject.Parse(response);
                response = responseObj["country"]?.ToString();

                return string.IsNullOrEmpty(response)
                    ? Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_WELCOME_UNKNOWN_COUNTRY"]
                    : response;
            }

            catch
            {
                return Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_WELCOME_UNKNOWN_IP"];
            }
        }
    }
}