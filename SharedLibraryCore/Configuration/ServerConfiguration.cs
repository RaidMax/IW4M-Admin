using SharedLibraryCore.Configuration.Attributes;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedLibraryCore.Configuration
{
    public class ServerConfiguration : IBaseConfiguration
    {
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_IP")]
        public string IPAddress { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_PORT")]
        public int Port { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_PASSWORD")]
        public string Password { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_RULES")]
        public List<string> Rules { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_AUTO_MESSAGES")]
        public List<string> AutoMessages { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_PATH")]
        [ConfigurationOptional]
        public string ManualLogPath { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_RCON_PARSER")]
        public string RConParserVersion { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_EVENT_PARSER")]
        public string EventParserVersion { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_RESERVED_SLOT")]
        public int ReservedSlotNumber { get; set; }
        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_GAME_LOG_SERVER")]
        [ConfigurationOptional]
        public Uri GameLogServerUrl { get; set; }

        private readonly IList<IRConParser> rconParsers;
        private readonly IList<IEventParser> eventParsers;

        public ServerConfiguration()
        {
            rconParsers = new List<IRConParser>();
            eventParsers = new List<IEventParser>();
            Rules = new List<string>();
            AutoMessages = new List<string>();
        }

        public void AddRConParser(IRConParser parser)
        {
            rconParsers.Add(parser);
        }

        public void AddEventParser(IEventParser parser)
        {
            eventParsers.Add(parser);
        }

        public void ModifyParsers()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var parserVersions = rconParsers.Select(_parser => _parser.Version).ToArray();
            var selection = Utilities.PromptSelection($"{loc["SETUP_SERVER_RCON_PARSER_VERSION"]} ({IPAddress}:{Port})", $"{loc["SETUP_PROMPT_DEFAULT"]} (Call of Duty)", null, parserVersions);

            if (selection.Item1 >= 0)
            {
                RConParserVersion = selection.Item2;

                if (selection.Item1 > 0 && !rconParsers[selection.Item1 - 1].CanGenerateLogPath)
                {
                    Console.WriteLine(loc["SETUP_SERVER_NO_LOG"]);
                    ManualLogPath = Utilities.PromptString(loc["SETUP_SERVER_LOG_PATH"]);
                }
            }

            parserVersions = eventParsers.Select(_parser => _parser.Version).ToArray();
            selection = Utilities.PromptSelection($"{loc["SETUP_SERVER_EVENT_PARSER_VERSION"]} ({IPAddress}:{Port})", $"{loc["SETUP_PROMPT_DEFAULT"]} (Call of Duty)", null, parserVersions);

            if (selection.Item1 >= 0)
            {
                EventParserVersion = selection.Item2;
            }
        }

        public IBaseConfiguration Generate()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            while (string.IsNullOrEmpty(IPAddress))
            {
                string input = Utilities.PromptString(loc["SETUP_SERVER_IP"]);

                if (System.Net.IPAddress.TryParse(input, out System.Net.IPAddress ip))
                {
                    IPAddress = input;
                }
            }

            Port = Utilities.PromptInt(loc["SETUP_SERVER_PORT"], null, 1, ushort.MaxValue);
            Password = Utilities.PromptString(loc["SETUP_SERVER_RCON"]);
            AutoMessages = new List<string>();
            Rules = new List<string>();
            ReservedSlotNumber = loc["SETUP_SERVER_RESERVEDSLOT"].PromptInt(null, 0, 32);
            ManualLogPath = null;

            ModifyParsers();

            return this;
        }

        public string Name()
        {
            return "ServerConfiguration";
        }
    }
}
