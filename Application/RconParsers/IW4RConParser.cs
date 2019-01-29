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

namespace IW4MAdmin.Application.RconParsers
{
    class IW4RConParser : IRConParser
    {
        public IW4RConParser()
        {
            Configuration = new DynamicRConParserConfiguration()
            {
                CommandPrefixes = new CommandPrefix()
                {
                    Tell = "tellraw {0} {1}",
                    Say = "sayraw {0}",
                    Kick = "clientkick {0} \"{1}\"",
                    Ban = "clientkick {0} \"{1}\"",
                    TempBan = "tempbanclient {0} \"{1}\""
                },
                GameName = Server.Game.IW4
            };

            Configuration.Status.Pattern = @"^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){16}|(?:[a-z]|[0-9]){32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +([0-9]+) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback) +(-*[0-9]+) +([0-9]+) *$";
            Configuration.Status.GroupMapping.Add(ParserRegex.GroupType.RConClientNumber, 1);
            Configuration.Status.GroupMapping.Add(ParserRegex.GroupType.RConScore, 2);
            Configuration.Status.GroupMapping.Add(ParserRegex.GroupType.RConPing, 3);
            Configuration.Status.GroupMapping.Add(ParserRegex.GroupType.RConNetworkId, 4);
            Configuration.Status.GroupMapping.Add(ParserRegex.GroupType.RConName, 5);
            Configuration.Status.GroupMapping.Add(ParserRegex.GroupType.RConIpAddress, 7);
        }

        public IRConParserConfiguration Configuration { get; set; }

        public async Task<string[]> ExecuteCommandAsync(Connection connection, string command)
        {
            var response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, command);
            return response.Skip(1).ToArray();
        }

        public async Task<Dvar<T>> GetDvarAsync<T>(Connection connection, string dvarName)
        {
            string[] LineSplit = await connection.SendQueryAsync(StaticHelpers.QueryType.DVAR, dvarName);

            if (LineSplit.Length < 3)
            {
                var e = new DvarException($"DVAR \"{dvarName}\" does not exist");
                e.Data["dvar_name"] = dvarName;
                throw e;
            }

            // todo: can this be made more portable and modifiable from plugin
            string[] ValueSplit = LineSplit[1].Split(new char[] { '"' }, StringSplitOptions.RemoveEmptyEntries);

            if (ValueSplit.Length < 5)
            {
                var e = new DvarException($"DVAR \"{dvarName}\" does not exist");
                e.Data["dvar_name"] = dvarName;
                throw e;
            }

            string DvarName = Regex.Replace(ValueSplit[0], @"\^[0-9]", "");
            string DvarCurrentValue = Regex.Replace(ValueSplit[2], @"\^[0-9]", "");
            string DvarDefaultValue = Regex.Replace(ValueSplit[4], @"\^[0-9]", "");

            return new Dvar<T>(DvarName)
            {
                Value = (T)Convert.ChangeType(DvarCurrentValue, typeof(T))
            };
        }

        public async Task<List<EFClient>> GetStatusAsync(Connection connection)
        {
            string[] response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, "status");
            return ClientsFromStatus(response);
        }

        public async Task<bool> SetDvarAsync(Connection connection, string dvarName, object dvarValue)
        {
            return (await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, $"set {dvarName} {dvarValue}")).Length > 0;
        }

        private List<EFClient> ClientsFromStatus(string[] Status)
        {
            List<EFClient> StatusPlayers = new List<EFClient>();

            if (Status.Length < 4)
            {
                throw new ServerException("Unexpected status response received");
            }

            int validMatches = 0;
            foreach (String S in Status)
            {
                String responseLine = S.Trim();

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

                    long networkId = regex.Groups[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConNetworkId]].Value.ConvertLong();
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
                        IsBot = ip == null,
                        State = EFClient.ClientState.Connecting
                    };

                    // they've not fully connected yet
                    if (!client.IsBot && ping == 999)
                    {
                        continue;
                    }

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
