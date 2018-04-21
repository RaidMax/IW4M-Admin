using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using SharedLibraryCore;
using SharedLibraryCore.RCon;
using SharedLibraryCore.Exceptions;

namespace Application.RconParsers
{
    class IW4RConParser : IRConParser
    {
        private static CommandPrefix Prefixes = new CommandPrefix()
        {
            Tell = "tellraw {0} {1}",
            Say = "sayraw {0}",
            Kick = "clientkick {0} \"{1}\"",
            Ban = "clientkick {0} \"{1}\"",
            TempBan = "tempbanclient {0} \"{1}\""
        };
       
        public async Task<string[]> ExecuteCommandAsync(Connection connection, string command)
        {
            return (await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, command)).Skip(1).ToArray();
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

        public async Task<List<Player>> GetStatusAsync(Connection connection)
        {
            string[] response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, "status");
            return ClientsFromStatus(response);
        }

        public async Task<bool> SetDvarAsync(Connection connection, string dvarName, object dvarValue)
        {
            return (await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, $"set {dvarName} {dvarValue}")).Length > 0;
        }

        public CommandPrefix GetCommandPrefixes() => Prefixes;

        private List<Player> ClientsFromStatus(string[] Status)
        {
            List<Player> StatusPlayers = new List<Player>();

            if (Status.Length < 4)
                throw new ServerException("Unexpected status response received");

            foreach (String S in Status)
            {
                String responseLine = S.Trim();

                if (Regex.Matches(responseLine, @" *^\d+", RegexOptions.IgnoreCase).Count > 0)
                {
                    String[] playerInfo = responseLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int cID = -1;
                    int Ping = -1;
                    Int32.TryParse(playerInfo[2], out Ping);
                    String cName = Encoding.UTF8.GetString(Encoding.Convert(Utilities.EncodingType, Encoding.UTF8, Utilities.EncodingType.GetBytes(responseLine.Substring(46, 18).StripColors().Trim())));
                    long npID = Regex.Match(responseLine, @"([a-z]|[0-9]){16}", RegexOptions.IgnoreCase).Value.ConvertLong();
                    int.TryParse(playerInfo[0], out cID);
                    var regex = Regex.Match(responseLine, @"\d+\.\d+\.\d+.\d+\:\d{1,5}");
                    int cIP = regex.Value.Split(':')[0].ConvertToIP();
                    regex = Regex.Match(responseLine, @"[0-9]{1,2}\s+[0-9]+\s+");
                    int score = Int32.Parse(regex.Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                    Player P = new Player()
                    {
                        Name = cName,
                        NetworkId = npID,
                        ClientNumber = cID,
                        IPAddress = cIP,
                        Ping = Ping,
                        Score = score,
                        IsBot = npID == -1
                    };
                    StatusPlayers.Add(P);
                }
            }

            return StatusPlayers;
        }
    }
}
