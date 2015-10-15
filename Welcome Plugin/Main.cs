using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharedLibrary;

namespace Welcome_Plugin
{
    public class Main : Plugin
    {
        public override string Author
        {
            get
            {
                return "RaidMax";
            }
        }

        public override float Version
        {
            get
            {
                return 1.0f;
            }
        }

        public override string Name
        {
            get
            {
                return "Welcome Plugin";
            }
        }

        public override void onEvent(Event E)
        {
            if (E.Type == Event.GType.Connect)
            {
                Player newPlayer = E.Origin;

                if (newPlayer.Level > Player.Permission.User)
                    E.Owner.Broadcast(Utilities.levelToColor(newPlayer.Level) + " ^5" + newPlayer.Name + " ^7has joined the server.");
  
                else
                {
                    CountryLookupProj.CountryLookup CLT = new CountryLookupProj.CountryLookup("GeoIP.dat");
                    E.Owner.Broadcast("^5" + newPlayer.Name + " ^7hails from ^5" + CLT.lookupCountryName(newPlayer.IP));
                }
            }
        }

        public override void onLoad()
        {
            return;
        }

        public override void onUnload()
        {
            return;
        }
    }
}
