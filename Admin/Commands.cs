using System;
using System.Collections.Generic;
using System.Text;
using SharedLibrary;
using SharedLibrary.Network;
using System.Threading.Tasks;

namespace IW4MAdmin
{
    class Plugins : Command
    {
        public Plugins(String N, String D, String U, Player.Permission P, int args, bool nT) : base(N, D, U, P, args, nT) { }

        public override async Task ExecuteAsync(Event E)
        {
            await E.Origin.Tell("^5Loaded Plugins:");
            foreach (SharedLibrary.Extensions.IPlugin P in PluginImporter.potentialPlugins)
            {
                await E.Origin.Tell(String.Format("^3{0} ^7[v^3{1}^7] by ^5{2}^7", P.Name, P.Version, P.Author));
            }
        }
    }
}
    