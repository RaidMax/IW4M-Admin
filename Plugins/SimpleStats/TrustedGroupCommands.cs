using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Helpers;

namespace StatsPlugin
{
    public class CEnableTrusted : Command
    {
        public CEnableTrusted() : base("enabletrusted", "enable trusted player group for the server. syntax: !enabletrusted", "et", Player.Permission.Owner, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            var config = new ConfigurationManager(E.Owner);
            if (config.GetProperty("EnableTrusted") == null)
                config.AddProperty(new KeyValuePair<string, object>("EnableTrusted", true));
            else
                config.UpdateProperty(new KeyValuePair<string, object>("EnableTrusted", true));

            await E.Origin.Tell("Trusted group has been disabled for this server");
        }
    }

    public class CDisableTrusted : Command
    {
        public CDisableTrusted() : base("disabletrusted", "disable trusted player group for the server. syntax: !disabletrusted", "dt", Player.Permission.Owner, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            var config = new ConfigurationManager(E.Owner);
            if (config.GetProperty("EnableTrusted") == null)
                config.AddProperty(new KeyValuePair<string, object>("EnableTrusted", false));
            else
                config.UpdateProperty(new KeyValuePair<string, object>("EnableTrusted", false));

            await E.Origin.Tell("Trusted group has been disabled for this server");
        }
    }
}
