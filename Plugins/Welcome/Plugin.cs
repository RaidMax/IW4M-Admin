using System;
using System.Threading.Tasks;

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Services;
using SharedLibraryCore.Database.Models;
using System.Linq;

namespace IW4MAdmin.Plugins.Welcome
{
    public class Plugin : IPlugin
    {
        String TimesConnected(Player P)
        {
            int connection = P.Connections;
            String Prefix = String.Empty;
            if (connection % 10 > 3 || connection % 10 == 0 || (connection % 100 > 9 && connection % 100 < 19))
                Prefix = "th";
            else
            {
                switch (connection % 10)
                {
                    case 1:
                        Prefix = "st";
                        break;
                    case 2:
                        Prefix = "nd";
                        break;
                    case 3:
                        Prefix = "rd";
                        break;
                }
            }

            switch (connection)
            {
                case 0:
                case 1:
                    return "first";
                case 2:
                    return "second";
                case 3:
                    return "third";
                case 4:
                    return "fourth";
                case 5:
                    return "fifth";
                default:
                    return connection.ToString() + Prefix;
            }
        }

        public string Author => "RaidMax";

        public float Version => 1.0f;

        public string Name => "Welcome Plugin";

        private BaseConfigurationHandler<WelcomeConfiguration> Config;

        public async Task OnLoadAsync(IManager manager)
        {
            // load custom configuration
            Config = new BaseConfigurationHandler<WelcomeConfiguration>("WelcomePluginSettings");
            if (Config.Configuration() == null)
            {
                Config.Set((WelcomeConfiguration)new WelcomeConfiguration().Generate());
                await Config.Save();
            }
        }

        public Task OnUnloadAsync() => Task.CompletedTask;

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public async Task OnEventAsync(GameEvent E, Server S)
        {
            if (E.Type == GameEvent.EventType.Connect)
            {
                Player newPlayer = E.Origin;
                if (newPlayer.Level >= Player.Permission.Trusted && !E.Origin.Masked)
                    E.Owner.Broadcast(ProcessAnnouncement(Config.Configuration().PrivilegedAnnouncementMessage, newPlayer));

                newPlayer.Tell(ProcessAnnouncement(Config.Configuration().UserWelcomeMessage, newPlayer));

                if (newPlayer.Level == Player.Permission.Flagged)
                {
                    var penalty = await new GenericRepository<EFPenalty>().FindAsync(p => p.OffenderId == newPlayer.ClientId && p.Type == Penalty.PenaltyType.Flag);
                    E.Owner.ToAdmins($"^1NOTICE: ^7Flagged player ^5{newPlayer.Name} ^7({penalty.FirstOrDefault()?.Offense}) has joined!");
                }
                else
                    E.Owner.Broadcast(ProcessAnnouncement(Config.Configuration().UserAnnouncementMessage, newPlayer));
            }
        }

        private string ProcessAnnouncement(string msg, Player joining)
        {
            msg = msg.Replace("{{ClientName}}", joining.Name);
            msg = msg.Replace("{{ClientLevel}}", Utilities.ConvertLevelToColor(joining.Level, joining.ClientPermission.Name));
            try
            {
                CountryLookupProj.CountryLookup CLT = new CountryLookupProj.CountryLookup($"{Utilities.OperatingDirectory}Plugins{System.IO.Path.DirectorySeparatorChar}GeoIP.dat");
                msg = msg.Replace("{{ClientLocation}}", CLT.LookupCountryName(joining.IPAddressString));
            }

            catch (Exception)
            {
                joining.CurrentServer.Manager.GetLogger().WriteError("Could not open file Plugins\\GeoIP.dat for Welcome Plugin");
            }
            msg = msg.Replace("{{TimesConnected}}", TimesConnected(joining));

            return msg;
        }
    }
}
