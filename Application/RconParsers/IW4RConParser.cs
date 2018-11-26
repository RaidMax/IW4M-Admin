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
        private static readonly CommandPrefix Prefixes = new CommandPrefix()
        {
            Tell = "tellraw {0} {1}",
            Say = "sayraw {0}",
            Kick = "clientkick {0} \"{1}\"",
            Ban = "clientkick {0} \"{1}\"",
            TempBan = "tempbanclient {0} \"{1}\""
        };

        private static readonly string StatusRegex = @"^( *[0-9]+) +-*([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){16}|(?:[a-z]|[0-9]){32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +([0-9]+) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback) +(-*[0-9]+) +([0-9]+) *$";

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

        public virtual CommandPrefix GetCommandPrefixes()
        {
            return Prefixes;
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

                var regex = Regex.Match(responseLine, StatusRegex, RegexOptions.IgnoreCase);

                if (regex.Success)
                {
                    validMatches++;
                    int clientNumber = int.Parse(regex.Groups[1].Value);
                    int score = int.Parse(regex.Groups[2].Value);

                    int ping = 999;

                    // their state can be CNCT, ZMBI etc
                    if (regex.Groups[3].Value.Length <= 3)
                    {
                        ping = int.Parse(regex.Groups[3].Value);
                    }

                    else
                    {
                        continue;
                    }

                    long networkId = regex.Groups[4].Value.ConvertLong();
                    string name = regex.Groups[5].Value.StripColors().Trim();
                    int ip = regex.Groups[7].Value.Split(':')[0].ConvertToIP();

                    var P = new EFClient()
                    {
                        CurrentAlias = new EFAlias()
                        {
                            Name = name
                        },
                        NetworkId = networkId,
                        ClientNumber = clientNumber,
                        IPAddress = ip == 0 ? int.MinValue : ip,
                        Ping = ping,
                        Score = score,
                        IsBot = ip == 0,
                        State = EFClient.ClientState.Connecting
                    };
                    StatusPlayers.Add(P);
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
