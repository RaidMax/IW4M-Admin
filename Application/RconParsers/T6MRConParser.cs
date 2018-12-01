using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using SharedLibraryCore.RCon;
using SharedLibraryCore.Exceptions;
using System.Text;
using SharedLibraryCore.Database.Models;

namespace IW4MAdmin.Application.RconParsers
{
    public class T6MRConParser : IRConParser
    {
        private static readonly CommandPrefix Prefixes = new CommandPrefix()
        {
            Tell = "tell {0} {1}",
            Say = "say {0}",
            Kick = "clientkick_for_reason {0} \"{1}\"",
            Ban = "clientkick_for_reason {0} \"{1}\"",
            TempBan = "clientkick_for_reason {0} \"{1}\""
        };

        public CommandPrefix GetCommandPrefixes() => Prefixes;

        public async Task<string[]> ExecuteCommandAsync(Connection connection, string command)
        {
            await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, command, false);
            return new string[] { "Command Executed" };
        }

        public async Task<Dvar<T>> GetDvarAsync<T>(Connection connection, string dvarName)
        {
            string[] LineSplit = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, $"get {dvarName}");

            if (LineSplit.Length < 2)
            {
                var e = new DvarException($"DVAR \"{dvarName}\" does not exist");
                e.Data["dvar_name"] = dvarName;
                throw e;
            }

            string[] ValueSplit = LineSplit[1].Split(new char[] { '"' });

            if (ValueSplit.Length == 0)
            {
                var e = new DvarException($"DVAR \"{dvarName}\" does not exist");
                e.Data["dvar_name"] = dvarName;
                throw e;
            }

            string DvarName = dvarName;
            string DvarCurrentValue = Regex.Replace(ValueSplit[1], @"\^[0-9]", "");

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
            // T6M doesn't respond with anything when a value is set, so we can only hope for the best :c
            await connection.SendQueryAsync(StaticHelpers.QueryType.DVAR, $"set {dvarName} {dvarValue}", false);
            return true;
        }

        private List<EFClient> ClientsFromStatus(string[] status)
        {
            List<EFClient> StatusPlayers = new List<EFClient>();

            foreach (string statusLine in status)
            {
                String responseLine = statusLine;

                if (Regex.Matches(responseLine, @"^ *\d+", RegexOptions.IgnoreCase).Count > 0) // its a client line!
                {
                    String[] playerInfo = responseLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int clientId = -1;
                    int Ping = -1;

                    Int32.TryParse(playerInfo[3], out Ping);
                    var regex = Regex.Match(responseLine, @"\^7.*\ +0 ");
                    string name = Encoding.UTF8.GetString(Encoding.Convert(Utilities.EncodingType, Encoding.UTF8, Utilities.EncodingType.GetBytes(regex.Value.Substring(0, regex.Value.Length - 2).StripColors().Trim())));
                    long networkId = playerInfo[4].ConvertLong();
                    int.TryParse(playerInfo[0], out clientId);
                    regex = Regex.Match(responseLine, @"\d+\.\d+\.\d+.\d+\:\d{1,5}");
#if DEBUG
                    Ping = 1;
#endif
                    int ipAddress = regex.Value.Split(':')[0].ConvertToIP();
                    regex = Regex.Match(responseLine, @"[0-9]{1,2}\s+[0-9]+\s+");
                    var p = new EFClient()
                    {
                        Name = name,
                        NetworkId = networkId,
                        ClientNumber = clientId,
                        IPAddress = ipAddress,
                        Ping = Ping,
                        Score = 0,
                        State = EFClient.ClientState.Connecting,
                        IsBot = networkId == 0
                    };

                    if (p.IsBot)
                        p.NetworkId = -p.ClientNumber;

                    StatusPlayers.Add(p);
                }
            }

            return StatusPlayers;
        }
    }
}
