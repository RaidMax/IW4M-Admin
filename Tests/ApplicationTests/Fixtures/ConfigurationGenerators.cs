using IW4MAdmin.Application.RconParsers;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationTests.Fixtures
{
    public class ConfigurationGenerators
    {
        public static ServerConfiguration CreateServerConfiguration() => new ServerConfiguration() { IPAddress = "127.0.0.1", Port = 28960 };
        public static IRConParserConfiguration CreateRConParserConfiguration(IParserRegexFactory factory) => new DynamicRConParserConfiguration(factory)
        {
            CommandPrefixes = new SharedLibraryCore.RCon.CommandPrefix()
            {
                Kick = "kick",
                Say = "say"
            }
        };
    }
}
