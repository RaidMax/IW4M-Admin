using System;
using SharedLibrary;
using SharedLibrary.Extensions;
using System.Threading.Tasks;

namespace Welcome_Plugin
{
    public class Plugin : IPlugin
    {
        public string Author
        {
            get
            {
                return "RaidMax";
            }
        }

        public float Version
        {
            get
            {
                return 1.0f;
            }
        }

        public string Name
        {
            get
            {
                return "Welcome Plugin";
            }
        }

        public async Task OnLoad()
        {
            return;
        }

        public async Task OnUnload()
        {
            return;
        }

        public async Task OnTick(Server S)
        {
            return;
        }

        public async Task OnEvent(Event E, Server S)
        {
            if (E.Type == Event.GType.Connect)
            {
                Player newPlayer = E.Origin;

                if (newPlayer.Level >= Player.Permission.Trusted && !E.Origin.Masked)
                   await  E.Owner.Broadcast(Utilities.levelToColor(newPlayer.Level) + " ^5" + newPlayer.Name + " ^7has joined the server.");

                if (newPlayer.Level == Player.Permission.Flagged)
                    await E.Owner.ToAdmins($"^1NOTICE: ^7Flagged player ^5{newPlayer.Name}^7 has joined!");

                else
                {
                    CountryLookupProj.CountryLookup CLT = new CountryLookupProj.CountryLookup("Plugins/GeoIP.dat");
                    await E.Owner.Broadcast($"^5{newPlayer.Name} ^7hails from ^5{CLT.lookupCountryName(newPlayer.IP)}");
                }
            }
        }
    }
}
