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
    public class BaseRConParser : IRConParser
    {
        public BaseRConParser(IParserRegexFactory parserRegexFactory)
        {
            Configuration = new DynamicRConParserConfiguration(parserRegexFactory)
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
                    RconGetInfoResponseHeader = "ÿÿÿÿinfoResponse"
                },
                ServerNotRunningResponse = "Server is not running."
            };

            Configuration.Status.Pattern = @"^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +([0-9]+) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback|unknown) +(-*[0-9]+) +([0-9]+) *$";
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

            Configuration.StatusHeader.Pattern = "num +score +ping +guid +name +lastmsg +address +qport +rate *";
            Configuration.GametypeStatus.Pattern = "";
            Configuration.MapStatus.Pattern = @"map: (([a-z]|_|\d)+)";
            Configuration.MapStatus.AddMapping(ParserRegex.GroupType.RConStatusMap, 1);

            if (!Configuration.DefaultDvarValues.ContainsKey("mapname"))
            {
                Configuration.DefaultDvarValues.Add("mapname", "Unknown");
            }
        }

        public IRConParserConfiguration Configuration { get; set; }
        public virtual string Version { get; set; } = "CoD";
        public Game GameName { get; set; } = Game.COD;
        public bool CanGenerateLogPath { get; set; } = true;
        public string Name { get; set; } = "Call of Duty";

        public async Task<string[]> ExecuteCommandAsync(IRConConnection connection, string command)
        {
            var response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, command);
            return response.Skip(1).ToArray();
        }

        public async Task<Dvar<T>> GetDvarAsync<T>(IRConConnection connection, string dvarName, T fallbackValue = default)
        {
            string[] lineSplit;

            try
            {
                lineSplit = await connection.SendQueryAsync(StaticHelpers.QueryType.GET_DVAR, dvarName);
            }
            catch
            {
                if (fallbackValue == null)
                {
                    throw;
                }

                lineSplit = new string[0];
            }

            string response = string.Join('\n', lineSplit).TrimEnd('\0');
            var match = Regex.Match(response, Configuration.Dvar.Pattern);

            if (response.Contains("Unknown command") ||
                !match.Success)
            {
                if (fallbackValue != null)
                {
                    return new Dvar<T>()
                    {
                        Name = dvarName,
                        Value = fallbackValue
                    };
                }

                throw new DvarException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_DVAR"].FormatExt(dvarName));
            }

            string value = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarValue]].Value;
            string defaultValue = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarDefaultValue]].Value;
            string latchedValue = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarLatchedValue]].Value;

            string removeTrailingColorCode(string input) => Regex.Replace(input, @"\^7$", "");

            value = removeTrailingColorCode(value);
            defaultValue = removeTrailingColorCode(defaultValue);
            latchedValue = removeTrailingColorCode(latchedValue);

            return new Dvar<T>()
            {
                Name = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarName]].Value,
                Value = string.IsNullOrEmpty(value) ? default : (T)Convert.ChangeType(value, typeof(T)),
                DefaultValue = string.IsNullOrEmpty(defaultValue) ? default : (T)Convert.ChangeType(defaultValue, typeof(T)),
                LatchedValue = string.IsNullOrEmpty(latchedValue) ? default : (T)Convert.ChangeType(latchedValue, typeof(T)),
                Domain = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarDomain]].Value
            };
        }

        public virtual async Task<(List<EFClient>, string, string)> GetStatusAsync(IRConConnection connection)
        {
            string[] response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND_STATUS);
#if DEBUG
            foreach (var line in response)
            {
                Console.WriteLine(line);
            }
#endif
            return (ClientsFromStatus(response), MapFromStatus(response), GameTypeFromStatus(response));
        }

        private string MapFromStatus(string[] response)
        {
            string map = null;
            foreach (var line in response)
            {
                var regex = Regex.Match(line, Configuration.MapStatus.Pattern);
                if (regex.Success)
                {
                    map = regex.Groups[Configuration.MapStatus.GroupMapping[ParserRegex.GroupType.RConStatusMap]].ToString();
                }
            }

            return map;
        }

        private string GameTypeFromStatus(string[] response)
        {
            if (string.IsNullOrWhiteSpace(Configuration.GametypeStatus.Pattern))
            {
                return null;
            }

            string gametype = null;
            foreach (var line in response)
            {
                var regex = Regex.Match(line, Configuration.GametypeStatus.Pattern);
                if (regex.Success)
                {
                    gametype = regex.Groups[Configuration.GametypeStatus.GroupMapping[ParserRegex.GroupType.RConStatusGametype]].ToString();
                }
            }

            return gametype;
        }

        public async Task<bool> SetDvarAsync(IRConConnection connection, string dvarName, object dvarValue)
        {
            string dvarString = (dvarValue is string str)
                ? $"{dvarName} \"{str}\""
                : $"{dvarName} {dvarValue.ToString()}";

            return (await connection.SendQueryAsync(StaticHelpers.QueryType.SET_DVAR, dvarString)).Length > 0;
        }

        private List<EFClient> ClientsFromStatus(string[] Status)
        {
            List<EFClient> StatusPlayers = new List<EFClient>();

            bool parsedHeader = false;
            foreach (string statusLine in Status)
            {
                string responseLine = statusLine.Trim();

                if (Configuration.StatusHeader.PatternMatcher.Match(responseLine).Success)
                {
                    parsedHeader = true;
                    continue;
                }

                var match = Configuration.Status.PatternMatcher.Match(responseLine);

                if (match.Success)
                {
                    int clientNumber = int.Parse(match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConClientNumber]]);
                    int score = int.Parse(match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConScore]]);

                    int ping = 999;

                    // their state can be CNCT, ZMBI etc
                    if (match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConPing]].Length <= 3)
                    {
                        ping = int.Parse(match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConPing]]);
                    }

                    long networkId;
                    string name = match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConName]].TrimNewLine();
                    string networkIdString;

                    try
                    {
                        networkIdString = match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConNetworkId]];

                        networkId = networkIdString.IsBotGuid() ?
                            name.GenerateGuidFromString() :
                            networkIdString.ConvertGuidToLong(Configuration.GuidNumberStyle);
                    }

                    catch (FormatException)
                    {
                        continue;
                    }

                    int? ip = match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConIpAddress]].Split(':')[0].ConvertToIP();

                    var client = new EFClient()
                    {
                        CurrentAlias = new EFAlias()
                        {
                            Name = name,
                            IPAddress = ip
                        },
                        NetworkId = networkId,
                        ClientNumber = clientNumber,
                        Ping = ping,
                        Score = score,
                        State = EFClient.ClientState.Connecting
                    };

                    client.SetAdditionalProperty("BotGuid", networkIdString);

                    StatusPlayers.Add(client);
                }
            }

            // this can happen if status is requested while map is rotating and we get a log dump back
            if (!parsedHeader)
            {
                throw new ServerException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_UNEXPECTED_STATUS"]);
            }

            return StatusPlayers;
        }

        public string GetOverrideDvarName(string dvarName)
        {
            if (Configuration.OverrideDvarNameMapping.ContainsKey(dvarName))
            {
                return Configuration.OverrideDvarNameMapping[dvarName];
            }

            return dvarName;
        }

        public T GetDefaultDvarValue<T>(string dvarName) => Configuration.DefaultDvarValues.ContainsKey(dvarName) ?
            (T)Convert.ChangeType(Configuration.DefaultDvarValues[dvarName], typeof(T)) :
            default;
    }
}
