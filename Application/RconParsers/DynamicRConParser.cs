using SharedLibraryCore;
using SharedLibraryCore.RCon;
using System;
using static SharedLibraryCore.Server;

namespace IW4MAdmin.Application.RconParsers
{
    sealed internal class DynamicRConParser : IW4RConParser
    {
        public string Version { get; set; }
    }
}
