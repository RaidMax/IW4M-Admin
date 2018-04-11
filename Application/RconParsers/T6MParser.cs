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

namespace Application.RconParsers
{
    public class T6MParser : IRConParser
    {
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
                String responseLine = statusLine.Trim();

                if (Regex.Matches(responseLine, @"\d+$", RegexOptions.IgnoreCase).Count > 0 && responseLine.Length > 72) // its a client line!
                {
                    String[] playerInfo = responseLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int clientId = -1;
                    int Ping = -1;

                    Int32.TryParse(playerInfo[3], out Ping);
                    string name = Encoding.UTF8.GetString(Encoding.Convert(Encoding.UTF7, Encoding.UTF8, Encoding.UTF7.GetBytes(responseLine.Substring(50, 15).StripColors().Trim())));
                    long networkId = playerInfo[4].ConvertLong();
                    int.TryParse(playerInfo[0], out clientId);
                    var regex = Regex.Match(responseLine, @"\d+\.\d+\.\d+.\d+\:\d{1,5}");
#if DEBUG
                    Ping = 1;
#endif
                    int ipAddress = regex.Value.Split(':')[0].ConvertToIP();
                    regex = Regex.Match(responseLine, @"[0-9]{1,2}\s+[0-9]+\s+");
                    int score = Int32.Parse(playerInfo[1]);

                    StatusPlayers.Add(new Player()
                    {
                        Name = name,
                        NetworkId = networkId,
                        ClientNumber = clientId,
                        IPAddress = ipAddress,
                        Ping = Ping,
                        Score = score
                    });
                }
            }

            return StatusPlayers;
        }
    }
}
