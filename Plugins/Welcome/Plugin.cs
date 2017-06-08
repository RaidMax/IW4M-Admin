using System;
using SharedLibrary;
using System.Collections.Generic;
using SharedLibrary.Interfaces;
using System.Threading.Tasks;

using SharedLibrary.Network;

namespace Welcome_Plugin
{
    public class Plugin : IPlugin
    {
        Dictionary<int, float> PlayerPings;
        int PingAverageCount;

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
            PlayerPings = new Dictionary<int, float>();
            PingAverageCount = 1;
        }

        public async Task OnUnloadAsync()
        {
            PlayerPings.Clear();
            PlayerPings = null;
        }

        public async Task OnTickAsync(Server S)
        {
            int MaxPing = (await S.GetDvarAsync<int>("sv_maxping")).Value;

            if (MaxPing == 0)
                return;

            foreach (int PlayerID in PlayerPings.Keys)
            {
                var Player = S.Players.Find(p => p.DatabaseID == PlayerID);
                PlayerPings[PlayerID] = PlayerPings[PlayerID] + (Player.Ping - PlayerPings[PlayerID]) / PingAverageCount;
                if (PlayerPings[PlayerID] > MaxPing)
                    await Player.Kick($"Your average ping of ^5{PlayerPings[PlayerID]} ^7is too high for this server", null);
            }

            if (PingAverageCount > 100)
                PingAverageCount = 1;
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Connect)
            {
                Player newPlayer = E.Origin;

                if (newPlayer.Level >= Player.Permission.Trusted && !E.Origin.Masked)
                    await E.Owner.Broadcast(Utilities.levelToColor(newPlayer.Level) + " ^5" + newPlayer.Name + " ^7has joined the server.");

                await newPlayer.Tell($"Welcome ^5{newPlayer.Name}^7, this your ^5{newPlayer.TimesConnected()} ^7time connecting!");

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

                PlayerPings.Add(E.Origin.DatabaseID, 1.0f);
            }

            if (E.Type == Event.GType.Disconnect)
            {
                PlayerPings.Remove(E.Origin.DatabaseID);
            }
        }
    }
}
