using System;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Objects;
using SharedLibrary.Configuration;

namespace Welcome_Plugin
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
                /*  case 100:
                        return "One-Hundreth (amazing!)";
                    case 500:
                        return "you're really ^5dedicated ^7to this server! This is your ^5500th^7";
                    case 1000:
                        return "you deserve a medal. it's your ^11000th^7";*/
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

        public Task OnUnloadAsync() => Utilities.CompletedTask;

        public Task OnTickAsync(Server S) => Utilities.CompletedTask;

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Connect)
            {
                Player newPlayer = E.Origin;
                if (newPlayer.Level >= Player.Permission.Trusted && !E.Origin.Masked)
                    await E.Owner.Broadcast(ProcessAnnouncement(Config.Configuration().PrivilegedAnnouncementMessage, newPlayer));

                await newPlayer.Tell(ProcessAnnouncement(Config.Configuration().UserWelcomeMessage, newPlayer));

                if (newPlayer.Level == Player.Permission.Flagged)
                    await E.Owner.ToAdmins($"^1NOTICE: ^7Flagged player ^5{newPlayer.Name} ^7has joined!");
                else
                    await E.Owner.Broadcast(ProcessAnnouncement(Config.Configuration().UserAnnouncementMessage, newPlayer));
            }
        }

        private string ProcessAnnouncement(string msg, Player joining)
        {
            msg = msg.Replace("{{ClientName}}", joining.Name);
            msg = msg.Replace("{{ClientLevel}}", Utilities.ConvertLevelToColor(joining.Level));
            try
            {
                CountryLookupProj.CountryLookup CLT = new CountryLookupProj.CountryLookup($"{Utilities.OperatingDirectory}Plugins{System.IO.Path.DirectorySeparatorChar}GeoIP.dat");
                msg = msg.Replace("{{ClientLocation}}", CLT.LookupCountryName(joining.IPAddressString));
            }

            catch (Exception)
            {
                joining.CurrentServer.Manager.GetLogger().WriteError("Could not open file Plugins/GeoIP.dat for Welcome Plugin");
            }
            msg = msg.Replace("{{TimesConnected}}", TimesConnected(joining));

            return msg;
        }
    }
}
