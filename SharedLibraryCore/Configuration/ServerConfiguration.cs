using System;
using System.Collections.Generic;
using System.Linq;
using SharedLibraryCore.Configuration.Attributes;
using SharedLibraryCore.Configuration.Extensions;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Configuration
{
    public class ServerConfiguration : IBaseConfiguration
    {
        private readonly IList<IRConParser> _rconParsers;
        private IRConParser _selectedParser;

        public ServerConfiguration()
        {
            _rconParsers = new List<IRConParser>();
            Rules = new string[0];
            AutoMessages = new string[0];
        }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_IP")]
        public string IPAddress { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_PORT")]
        public int Port { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_PASSWORD")]
        public string Password { get; set; }

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_RULES")]
        public string[] Rules { get; set; } = new string[0];

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_AUTO_MESSAGES")]
        public string[] AutoMessages { get; set; } = new string[0];

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

        [LocalizedDisplayName("WEBFRONT_CONFIGURATION_SERVER_CUSTOM_HOSTNAME")]
        [ConfigurationOptional]
        public string CustomHostname { get; set; }
        public string PerformanceBucket { get; set; }

        public IBaseConfiguration Generate()
        {
            ModifyParsers();
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var shouldTryFindIp = loc["SETUP_SERVER_IP_AUTO"].PromptBool(defaultValue: true);

            if (shouldTryFindIp)
            {
                this.TrySetIpAddress();
                Console.WriteLine(loc["SETUP_SERVER_IP_AUTO_RESULT"].FormatExt(IPAddress));
            }

            else
            {
                while (string.IsNullOrEmpty(IPAddress))
                {
                    var input = loc["SETUP_SERVER_IP"].PromptString();
                    IPAddress = input;
                }
            }

            var defaultPort = _selectedParser.Configuration.DefaultRConPort;
            Port = loc["SETUP_SERVER_PORT"].PromptInt(null, 1, ushort.MaxValue, defaultPort);

            if (!string.IsNullOrEmpty(_selectedParser.Configuration.DefaultInstallationDirectoryHint))
            {
                var shouldTryFindPassword = loc["SETUP_RCON_PASSWORD_AUTO"].PromptBool(defaultValue: true);

                if (shouldTryFindPassword)
                {
                    var passwords = _selectedParser.TryGetRConPasswords();
                    if (passwords.Length > 1)
                    {
                        var (index, value) =
                            loc["SETUP_RCON_PASSWORD_PROMPT"].PromptSelection(loc["SETUP_RCON_PASSWORD_MANUAL"], null,
                                passwords.Select(pw =>
                                        $"{pw.Item1}{(string.IsNullOrEmpty(pw.Item2) ? "" : "        " + pw.Item2)}")
                                    .ToArray());

                        if (index > 0)
                        {
                            Password = passwords[index - 1].Item1;
                        }
                    }

                    else if (passwords.Length > 0)
                    {
                        Password = passwords[0].Item1;
                        Console.WriteLine(loc["SETUP_RCON_PASSWORD_RESULT"].FormatExt(Password));
                    }
                }
            }

            if (string.IsNullOrEmpty(Password))
            {
                Password = loc["SETUP_SERVER_RCON"].PromptString();
            }

            AutoMessages = new string[0];
            Rules = new string[0];
            ManualLogPath = null;

            return this;
        }

        public string Name()
        {
            return "ServerConfiguration";
        }

        public void AddRConParser(IRConParser parser)
        {
            _rconParsers.Add(parser);
        }

        public void AddEventParser(IEventParser parser)
        {
        }

        public void ModifyParsers()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var parserVersions = _rconParsers.Select(p => p.Name).ToArray();
            var (index, parser) = loc["SETUP_SERVER_RCON_PARSER_VERSION"].PromptSelection(parserVersions[0],
                null, parserVersions);

            if (index < 0)
            {
                return;
            }

            _selectedParser = _rconParsers.FirstOrDefault(p => p.Name == parser);
            RConParserVersion = _selectedParser?.Name;
            EventParserVersion = _selectedParser?.Name;

            if (index <= 0 || _rconParsers[index].CanGenerateLogPath)
            {
                return;
            }

            Console.WriteLine(loc["SETUP_SERVER_NO_LOG"]);
            ManualLogPath = loc["SETUP_SERVER_LOG_PATH"].PromptString();
        }
    }
}
