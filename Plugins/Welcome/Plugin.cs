using System;
using SharedLibrary;
using System.Collections.Generic;
using SharedLibrary.Interfaces;
using System.Threading.Tasks;

using SharedLibrary.Network;
using SharedLibrary.Objects;
using SharedLibrary.Helpers;

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

        private Dictionary<int, ConfigurationManager> Configs;

        public async Task OnLoadAsync(IManager manager)
        {
            await Task.FromResult(Configs = new Dictionary<int, ConfigurationManager>());
        }

        public async Task OnUnloadAsync()
        {
        }

        public async Task OnTickAsync(Server S)
        {
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Connect)
            {
                Player newPlayer = E.Origin;
                var cfg = Configs[S.GetHashCode()];
                if (newPlayer.Level >= Player.Permission.Trusted && !E.Origin.Masked)
                    await E.Owner.Broadcast(ProcessAnnouncement(cfg.GetProperty<string>("PrivilegedAnnouncementMessage"), newPlayer));

                await newPlayer.Tell(ProcessAnnouncement(cfg.GetProperty<string>("UserWelcomeMessage"), newPlayer));

                if (newPlayer.Level == Player.Permission.Flagged)
                    await E.Owner.ToAdmins($"^1NOTICE: ^7Flagged player ^5{newPlayer.Name} ^7has joined!");
                else
                    await E.Owner.Broadcast(ProcessAnnouncement(cfg.GetProperty<string>("UserAnnouncementMessage"), newPlayer));
            }

            if (E.Type == Event.GType.Start)
            {
                var cfg = new ConfigurationManager(S);
                Configs.Add(S.GetHashCode(), cfg);
                if (cfg.GetProperty<string>("UserWelcomeMessage") == null)
                {
                    string welcomeMsg = "Welcome ^5{{ClientName}}^7, this is your ^5{{TimesConnected}} ^7time connecting!";
                    cfg.AddProperty(new KeyValuePair<string, dynamic>("UserWelcomeMessage", welcomeMsg));
                }

                if (cfg.GetProperty<string>("PrivilegedAnnouncementMessage") == null)
                {
                    string annoucementMsg = "{{ClientLevel}} {{ClientName}} has joined the server";
                    cfg.AddProperty(new KeyValuePair<string, dynamic>("PrivilegedAnnouncementMessage", annoucementMsg));
                }

                if (cfg.GetProperty<string>("UserAnnouncementMessage") == null)
                {
                    string annoucementMsg = "^5{{ClientName}} ^7hails from ^5{{ClientLocation}}";
                    cfg.AddProperty(new KeyValuePair<string, dynamic>("UserAnnouncementMessage", annoucementMsg));
                }
            }
        }

        private string ProcessAnnouncement(string msg, Player joining)
        {
            msg = msg.Replace("{{ClientName}}", joining.Name);
            msg = msg.Replace("{{ClientLevel}}", Utilities.ConvertLevelToColor(joining.Level));
            try
            {
                CountryLookupProj.CountryLookup CLT = new CountryLookupProj.CountryLookup("Plugins/GeoIP.dat");
                msg = msg.Replace("{{ClientLocation}}", CLT.lookupCountryName(joining.IPAddressString));
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
