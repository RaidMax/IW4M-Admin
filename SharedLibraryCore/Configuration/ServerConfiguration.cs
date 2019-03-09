using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedLibraryCore.Configuration
{
    public class ServerConfiguration : IBaseConfiguration
    {
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public string Password { get; set; }
        public IList<string> Rules { get; set; }
        public IList<string> AutoMessages { get; set; }
        public string ManualLogPath { get; set; }
        public string RConParserVersion { get; set; }
        public string EventParserVersion { get; set; }
        public int ReservedSlotNumber { get; set; }
        public Uri GameLogServerUrl { get; set; }

        private readonly IList<IRConParser> rconParsers;
        private readonly IList<IEventParser> eventParsers;

        public ServerConfiguration()
        {
            rconParsers = new List<IRConParser>();
            eventParsers = new List<IEventParser>();
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

            if (selection.Item1 > 0)
            {
                RConParserVersion = selection.Item2;

                if (!rconParsers[selection.Item1 - 1].CanGenerateLogPath)
                {
                    Console.WriteLine(loc["SETUP_SERVER_NO_LOG"]);
                    ManualLogPath = Utilities.PromptString(loc["SETUP_SERVER_LOG_PATH"]);
                }
            }

            parserVersions = eventParsers.Select(_parser => _parser.Version).ToArray();
            selection = Utilities.PromptSelection($"{loc["SETUP_SERVER_EVENT_PARSER_VERSION"]} ({IPAddress}:{Port})", $"{loc["SETUP_PROMPT_DEFAULT"]} (Call of Duty)", null, parserVersions);

            if (selection.Item1 > 0)
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

            Port = Utilities.PromptInt(Utilities.PromptString(loc["SETUP_SERVER_PORT"]), null, 1, ushort.MaxValue);
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
