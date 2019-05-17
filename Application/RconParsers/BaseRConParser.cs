using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SharedLibraryCore.Server;

namespace IW4MAdmin.Application.RconParsers
{
    class BaseRConParser : IRConParser
    {
        public BaseRConParser()
        {
            Configuration = new DynamicRConParserConfiguration()
            {
                CommandPrefixes = new CommandPrefix()
                {
                    Tell = "tell {0} {1}",
                    Say = "say {0}",
                    Kick = "clientkick {0} \"{1}\"",
                    Ban = "clientkick {0} \"{1}\"",
                    TempBan = "tempbanclient {0} \"{1}\"",
                    RConCommand = "ÿÿÿÿrcon {0} {1}",
                    RConGetDvar = "ÿÿÿÿrcon {0} {1}",
                    RConSetDvar = "ÿÿÿÿrcon {0} set {1}",
                    RConGetStatus = "ÿÿÿÿgetstatus",
                    RConGetInfo = "ÿÿÿÿgetinfo",
                    RConResponse = "ÿÿÿÿprint",
                },
            };

            Configuration.Status.Pattern = @"^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) +(.{0,32}) +([0-9]+) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback) +(-*[0-9]+) +([0-9]+) *$";
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConClientNumber, 1);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConScore, 2);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConPing, 3);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 4);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConName, 5);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 7);

            Configuration.Dvar.Pattern = "^\"(.+)\" is: \"(.+)?\" default: \"(.+)?\"\n(?:latched: \"(.+)?\"\n)? *(.+)$";
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarName, 1);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarValue, 2);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarDefaultValue, 3);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarLatchedValue, 4);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarDomain, 5);
        }

        public IRConParserConfiguration Configuration { get; set; }

        public string Version { get; set; } = "CoD";
        public Game GameName { get; set; } = Game.COD;
        public bool CanGenerateLogPath { get; set; } = true;

        public async Task<string[]> ExecuteCommandAsync(Connection connection, string command)
        {
            var response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, command);
            return response.Skip(1).ToArray();
        }

        public async Task<Dvar<T>> GetDvarAsync<T>(Connection connection, string dvarName)
        {
            string[] lineSplit = await connection.SendQueryAsync(StaticHelpers.QueryType.GET_DVAR, dvarName);
            string response = string.Join('\n', lineSplit.Skip(1));
            var match = Regex.Match(response, Configuration.Dvar.Pattern);

            if (!lineSplit[0].Contains(Configuration.CommandPrefixes.RConResponse) ||
                response.Contains("Unknown command") ||
                !match.Success)
            {
                throw new DvarException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_DVAR"].FormatExt(dvarName));
            }

            string value = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarValue]].Value.StripColors();
            string defaultValue = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarDefaultValue]].Value.StripColors();
            string latchedValue = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarLatchedValue]].Value.StripColors();

            return new Dvar<T>()
            {
                Name = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarName]].Value.StripColors(),
                Value = string.IsNullOrEmpty(value) ? default : (T)Convert.ChangeType(value, typeof(T)),
                DefaultValue = string.IsNullOrEmpty(defaultValue) ? default : (T)Convert.ChangeType(defaultValue, typeof(T)),
                LatchedValue = string.IsNullOrEmpty(latchedValue) ? default : (T)Convert.ChangeType(latchedValue, typeof(T)),
                Domain = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarDomain]].Value.StripColors()
            };
        }

        public async Task<List<EFClient>> GetStatusAsync(Connection connection)
        {
            string[] response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND_STATUS);
            return ClientsFromStatus(response);
        }

        public async Task<bool> SetDvarAsync(Connection connection, string dvarName, object dvarValue)
        {
            return (await connection.SendQueryAsync(StaticHelpers.QueryType.SET_DVAR, $"{dvarName} {dvarValue}")).Length > 0;
        }

        private List<EFClient> ClientsFromStatus(string[] Status)
        {
            List<EFClient> StatusPlayers = new List<EFClient>();

            if (Status.Length < 4)
            {
                throw new ServerException("Unexpected status response received");
            }

            int validMatches = 0;
            foreach (string statusLine in Status)
            {
                string responseLine = statusLine.Trim();

                var regex = Regex.Match(responseLine, Configuration.Status.Pattern, RegexOptions.IgnoreCase);

                if (regex.Success)
                {
                    validMatches++;
                    int clientNumber = int.Parse(regex.Groups[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConClientNumber]].Value);
                    int score = int.Parse(regex.Groups[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConScore]].Value);

                    int ping = 999;

                    // their state can be CNCT, ZMBI etc
                    if (regex.Groups[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConPing]].Value.Length <= 3)
                    {
                        ping = int.Parse(regex.Groups[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConPing]].Value);
                    }

                    long networkId;
                    try
                    {
                        networkId = regex.Groups[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConNetworkId]].Value.ConvertGuidToLong();
                    }

                    catch (FormatException)
                    {
                        continue;
                    }

                    string name = regex.Groups[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConName]].Value.StripColors().Trim();
                    int? ip = regex.Groups[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConIpAddress]].Value.Split(':')[0].ConvertToIP();

                    var client = new EFClient()
                    {
                        CurrentAlias = new EFAlias()
                        {
                            Name = name
                        },
                        NetworkId = networkId,
                        ClientNumber = clientNumber,
                        IPAddress = ip,
                        Ping = ping,
                        Score = score,
                        State = EFClient.ClientState.Connecting
                    };

                    StatusPlayers.Add(client);
                }
            }

            // this happens if status is requested while map is rotating
            if (Status.Length > 5 && validMatches == 0)
            {
                throw new ServerException("Server is rotating map");
            }

            return StatusPlayers;
        }
    }
}
