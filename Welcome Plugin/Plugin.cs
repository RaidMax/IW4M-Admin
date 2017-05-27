using System;
using SharedLibrary;
using SharedLibrary.Interfaces;
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

        public async Task OnLoadAsync()
        {
            return;
        }

        public async Task OnUnloadAsync()
        {
            return;
        }

        public async Task OnTickAsync(Server S)
        {
            return;
        }

        public async Task OnEventAsync(Event E, Server S)
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
                    try
                    {
                        CountryLookupProj.CountryLookup CLT = new CountryLookupProj.CountryLookup("Plugins/GeoIP.dat");
                        await E.Owner.Broadcast($"^5{newPlayer.Name} ^7hails from ^5{CLT.lookupCountryName(newPlayer.IP)}");
                    }

                    catch (Exception)
                    {
                        E.Owner.Manager.GetLogger().WriteError("Could not open file Plugins/GeoIP.dat for Welcome Plugin");
                    }
                    
                }
            }
        }
    }
}
