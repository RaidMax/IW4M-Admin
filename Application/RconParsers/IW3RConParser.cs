using SharedLibraryCore.RCon;
using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application.RconParsers
{
    class IW3RConParser : IW4RConParser
    {
        private static readonly CommandPrefix Prefixes = new CommandPrefix()
        {
            Tell = "tell {0} {1}",
            Say = "say {0}",
            Kick = "clientkick {0} \"{1}\"",
            Ban = "clientkick {0} \"{1}\"",
            TempBan = "tempbanclient {0} \"{1}\""
        };

        public override CommandPrefix GetCommandPrefixes() => Prefixes;
    }
}
