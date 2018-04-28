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
using System.Linq;
using System.Net.Http;

namespace Application.RconParsers
{
    public class T6MRConParser : IRConParser
    {
        class T6MResponse
        {
            public class SInfo
            {
                public short Com_maxclients { get; set; }
                public string Game { get; set; }
                public string Gametype { get; set; }
                public string Mapname { get; set; }
                public short NumBots { get; set; }
                public short NumClients { get; set; }
                public short Round { get; set; }
                public string Sv_hostname { get; set; }
            }

            public class PInfo
            {
                public short Assists { get; set; }
                public string Clan { get; set; }
                public short Deaths { get; set; }
                public short Downs { get; set; }
                public short Headshots { get; set; }
                public short Id { get; set; }
                public bool IsBot { get; set; }
                public short Kills { get; set; }
                public string Name { get; set; }
                public short Ping { get; set; }
                public short Revives { get; set; }
                public int Score { get; set; }
                public long Xuid { get; set; }
                public string Ip { get; set; }
            }

            public SInfo Info { get; set; }
            public PInfo[] Players { get; set; }
        }

        private static CommandPrefix Prefixes = new CommandPrefix()
        {
            Tell = "tell {0} {1}",
            Say = "say {0}",
            Kick = "clientKick {0}",
            Ban = "clientKick {0}",
            TempBan = "clientKick {0}"
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

        public async Task<List<Player>> GetStatusAsync(Connection connection)
        {
            string[] response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, "status");
            return ClientsFromStatus(response);

            //return ClientsFromResponse(connection);
        }

        public async Task<bool> SetDvarAsync(Connection connection, string dvarName, object dvarValue)
        {
            // T6M doesn't respond with anything when a value is set, so we can only hope for the best :c
            await connection.SendQueryAsync(StaticHelpers.QueryType.DVAR, $"set {dvarName} {dvarValue}", false);
            return true;
        }

        private async Task<List<Player>> ClientsFromResponse(Connection conn)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri($"http://{conn.Endpoint.Address}:{conn.Endpoint.Port}/");

                try
                {
                    var parameters = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("rcon_password", conn.RConPassword)
                    });

                    var serverResponse = await client.PostAsync("/info", parameters);
                    var serverResponseObject = Newtonsoft.Json.JsonConvert.DeserializeObject<T6MResponse>(await serverResponse.Content.ReadAsStringAsync());

                    return serverResponseObject.Players.Select(p => new Player()
                    {
                        Name = p.Name,
                        NetworkId = p.Xuid,
                        ClientNumber = p.Id,
                        IPAddress = p.Ip.Split(':')[0].ConvertToIP(),
                        Ping = p.Ping,
                        Score = p.Score,
                        IsBot = p.IsBot,
                    }).ToList();
                }

                catch (HttpRequestException e)
                {
                    throw new NetworkException(e.Message);
                }
            }
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
                    int score = 0;
                    // todo: fix this when T6M score is valid ;)
                    //int score = Int32.Parse(playerInfo[1]);
                    var p = new Player()
                    {
                        Name = name,
                        NetworkId = networkId,
                        ClientNumber = clientId,
                        IPAddress = ipAddress,
                        Ping = Ping,
                        Score = score,
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
