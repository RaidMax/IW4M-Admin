using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text;

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using SharedLibraryCore.RCon;
using SharedLibraryCore.Exceptions;

namespace IW4MAdmin.Application.RconParsers
{
    public class IW5MRConParser : IRConParser
    {
        private static readonly CommandPrefix Prefixes = new CommandPrefix()
        {
            Tell = "tell {0} {1}",
            Say = "say {0}",
            Kick = "dropClient {0} \"{1}\"",
            Ban = "dropClient {0} \"{1}\"",
            TempBan = "dropClient {0} \"{1}\""
        };

        public CommandPrefix GetCommandPrefixes() => Prefixes;

        public async Task<string[]> ExecuteCommandAsync(Connection connection, string command)
        {
            await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, command, false);
            return new string[] { "Command Executed" };
        }

        public async Task<Dvar<T>> GetDvarAsync<T>(Connection connection, string dvarName)
        {
            // why can't this be real :(
            if (dvarName == "version")
                return new Dvar<T>(dvarName)
                {
                    Value = (T)Convert.ChangeType("IW5 MP 1.9 build 461 Fri Sep 14 00:04:28 2012 win-x86", typeof(T))
                };

            if (dvarName == "shortversion")
                return new Dvar<T>(dvarName)
                {
                    Value = (T)Convert.ChangeType("1.9", typeof(T))
                };

            if (dvarName == "mapname")
                return new Dvar<T>(dvarName)
                {
                    Value = (T)Convert.ChangeType("Unknown", typeof(T))
                };

            if (dvarName == "g_gametype")
                return new Dvar<T>(dvarName)
                {
                    Value = (T)Convert.ChangeType("Unknown", typeof(T))
                };

            if (dvarName == "fs_game")
                return new Dvar<T>(dvarName)
                {
                    Value = (T)Convert.ChangeType("", typeof(T))
                };

            if (dvarName == "g_logsync")
                return new Dvar<T>(dvarName)
                {
                    Value = (T)Convert.ChangeType(1, typeof(T))
                };

            if (dvarName == "fs_basepath")
                return new Dvar<T>(dvarName)
                {
                    Value = (T)Convert.ChangeType("", typeof(T))
                };



            string[] LineSplit = await connection.SendQueryAsync(StaticHelpers.QueryType.DVAR, dvarName);

            if (LineSplit.Length < 4)
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
            string DvarCurrentValue = Regex.Replace(ValueSplit[3].StripColors(), @"\^[0-9]", "");

            return new Dvar<T>(DvarName)
            {
                Value = (T)Convert.ChangeType(DvarCurrentValue, typeof(T))
            };
        }

        public async Task<List<Player>> GetStatusAsync(Connection connection)
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

        private List<Player> ClientsFromStatus(string[] status)
        {
            List<Player> StatusPlayers = new List<Player>();

            foreach (string statusLine in status)
            {
                String responseLine = statusLine;

                if (Regex.Matches(responseLine, @"^ *\d+", RegexOptions.IgnoreCase).Count > 0) // its a client line!
                {
                    String[] playerInfo = responseLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // this happens when the client is in a zombie state
                    if (playerInfo.Length < 5)
                        continue;
                    int clientId = -1;
                    int Ping = -1;

                    Int32.TryParse(playerInfo[2], out Ping);
                    string name = Encoding.UTF8.GetString(Encoding.Convert(Utilities.EncodingType, Encoding.UTF8, Utilities.EncodingType.GetBytes(responseLine.Substring(23, 15).StripColors().Trim())));
                    long networkId = 0;//playerInfo[4].ConvertLong();
                    int.TryParse(playerInfo[0], out clientId);
                    var regex = Regex.Match(responseLine, @"\d+\.\d+\.\d+.\d+\:\d{1,5}");
                    int ipAddress = regex.Value.Split(':')[0].ConvertToIP();
                    regex = Regex.Match(responseLine, @" +(\d+ +){3}");
                    int score = Int32.Parse(regex.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);

                    var p = new Player()
                    {
                        Name = name,
                        NetworkId = networkId,
                        ClientNumber = clientId,
                        IPAddress = ipAddress,
                        Ping = Ping,
                        Score = score,
                        IsBot = false,
                        State = Player.ClientState.Connecting
                    };

                    StatusPlayers.Add(p);

                    if (p.IsBot)
                        p.NetworkId = -p.ClientNumber;
                }
            }

            return StatusPlayers;
        }
    }
}
