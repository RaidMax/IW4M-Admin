#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Helpers;
using SharedLibrary.Objects;
using System.Text.RegularExpressions;
using StatsPlugin.Models;
using SharedLibrary.Services;
using SharedLibrary.Database.Models;
using SharedLibrary.Database;

namespace IW4MAdmin.Plugins
{
    public class Tests : IPlugin
    {
        public string Name => "Dev Tests";

        public float Version => 0.1f;

        public string Author => "RaidMax";

        private DateTime Interval;

        public async Task OnEventAsync(Event E, Server S)
        {
            return;
            if (E.Type == Event.GType.Start)
            {
                #region PLAYER_HISTORY
                var rand = new Random(GetHashCode());
                var time = DateTime.UtcNow;

                await Task.Run(() =>
                 {
                     if (S.PlayerHistory.Count > 0)
                         return;

                     while (S.PlayerHistory.Count < 144)
                     {
                         S.PlayerHistory.Enqueue(new PlayerHistory(time, rand.Next(7, 18)));
                         time = time.AddMinutes(PlayerHistory.UpdateInterval);
                     }
                 });
                #endregion

                #region PLUGIN_INFO
                Console.WriteLine("|Name              |Alias|Description                                                                               |Requires Target|Syntax           |Required Level|");
                Console.WriteLine("|--------------| -----| --------------------------------------------------------| -----------------| -------------| ----------------|");
                foreach (var command in S.Manager.GetCommands().OrderByDescending(c => c.Permission).ThenBy(c => c.Name))
                {
                    Console.WriteLine($"|{command.Name}|{command.Alias}|{command.Description}|{command.RequiresTarget}|{command.Syntax.Substring(8).EscapeMarkdown()}|{command.Permission}|");
                }
                #endregion
            }
        }

        public async Task OnLoadAsync(IManager manager)
        {
          //  #if DO_IMPORT
            var svc = new GenericRepository<EFServer>();
            svc.Insert(new EFServer()
            {
                Active = true,
                Port = 28960,
                ServerId = Math.Abs("127.0.0.1:28960".GetHashCode()),
            });

            svc.Insert(new EFServer()
            {
                Active = true,
                Port = 28965,
                ServerId = Math.Abs("127.0.0.1:28965".GetHashCode()),
            });

            svc.Insert(new EFServer()
            {
                Active = true,
                Port = 28970,
                ServerId = Math.Abs("127.0.0.1:28970".GetHashCode()),
            });

            svc.SaveChanges();
     //       #endif
            Interval = DateTime.Now;
            var clients = new List<Player>();
            var oldClients = new Dictionary<int, Player>();
            #region CLIENTS
            if (File.Exists("import_clients.csv"))
            {
                manager.GetLogger().WriteVerbose("Beginning import of existing clients");

                var lines = File.ReadAllLines("import_clients.csv").Skip(1);
                foreach (string line in lines)
                {
                    string[] fields = Regex.Replace(line, "\".*\"", "").Split(',');
                    fields.All(f =>
                                {
                                    f = f.StripColors().Trim();
                                    return true;
                                });

                    if (fields.Length != 11)
                    {
                        manager.GetLogger().WriteError("Invalid client import file... aborting import");
                        return;
                    }

                    if (fields[1].Substring(0, 5) == "01100" || fields[0] == string.Empty || fields[1] == string.Empty || fields[6] == string.Empty)
                        continue;

                    if (!Regex.Match(fields[6], @"^\d+\.\d+\.\d+.\d+$").Success)
                        fields[6] = "0";

                    var client = new Player()
                    {
                        Name = fields[0],
                        NetworkId = fields[1].ConvertLong(),
                        IPAddress = fields[6].ConvertToIP(),
                        Level = (Player.Permission)Convert.ToInt32(fields[3]),
                        Connections = Convert.ToInt32(fields[5]),
                        LastConnection = DateTime.Parse(fields[7]),
                    };

                    clients.Add(client);
                    oldClients.Add(Convert.ToInt32(fields[2]), client);
                }
                clients = clients.Distinct().ToList();
                             //   #if DO_IMPORT

                /*clients = clients
                    .GroupBy(c => new { c.Name, c.IPAddress })
                                    .Select(c => c.FirstOrDefault())
                                    .ToList();*/

                //newClients = clients.ToList();
                //newClients.ForEach(c => c.ClientId = 0);

                manager.GetLogger().WriteVerbose($"Read {clients.Count} clients for import");

                try
                {
                    SharedLibrary.Database.Importer.ImportClients(clients);
                }

                catch (Exception e)
                {
                    manager.GetLogger().WriteError("Saving imported clients failed");
                }
              //  #endif
            }
#endregion
            // load the entire database lol
            var ctx = new DatabaseContext();
            ctx.Configuration.ProxyCreationEnabled = false;
            var cls = ctx.Clients.Include("AliasLink.Children").ToList(); //manager.GetClientService().Find(c => c.Active).Result;
            ctx.Dispose();

#region ALIASES
            if (File.Exists("import_aliases.csv"))
            {
                manager.GetLogger().WriteVerbose("Beginning import of existing aliases");

                var aliases = new List<EFAlias>();

                var lines = File.ReadAllLines("import_aliases.csv").Skip(1);
                foreach (string line in lines)
                {
                    string[] fields = Regex.Replace(line, "\".*\"", "").Split(',');
                    fields.All(f =>
                    {
                        f = f.StripColors().Trim();
                        return true;
                    });

                    if (fields.Length != 3)
                    {
                        manager.GetLogger().WriteError("Invalid alias import file... aborting import");
                        return;
                    }
                    try
                    {
                        int number = Int32.Parse(fields[0]);
                        var names = fields[1].Split(';').Where(n => n != String.Empty && n.Length > 2);

                        var oldClient = oldClients[number];
                        var newClient = cls.FirstOrDefault(c => c.NetworkId == oldClient.NetworkId);

                        foreach (string name in names)
                        {
                            // this is slow :D
                            if (newClient.AliasLink.Children.FirstOrDefault(n => n.Name == name) != null) continue;
                            var alias = new EFAlias()
                            {
                                Active = true,
                                DateAdded = DateTime.UtcNow,
                                Name = name,
                                LinkId = newClient.AliasLinkId,
                                IPAddress = newClient.IPAddress
                            };

                            aliases.Add(alias);
                        }
                    }
                    catch (KeyNotFoundException)
                    {
                        continue;
                    }

                    catch (Exception)
                    {
                        manager.GetLogger().WriteVerbose($"Could not import alias with line {line}");
                    }
                }

                SharedLibrary.Database.Importer.ImportSQLite(aliases);
            }
#endregion
#region PENALTIES
            if (File.Exists("import_penalties.csv"))
            {
                var penalties = new List<Penalty>();
                manager.GetLogger().WriteVerbose("Beginning import of existing penalties");
                foreach (string line in File.ReadAllLines("import_penalties.csv").Skip(1))
                {
                    string comma = Regex.Match(line, "\".*,.*\"").Value.Replace(",", "");
                    string[] fields = Regex.Replace(line, "\".*,.*\"", comma).Split(',');

                    fields.All(f =>
                                        {
                                            f = f.StripColors().Trim();
                                            return true;
                                        });

                    if (fields.Length != 7)
                    {
                        manager.GetLogger().WriteError("Invalid penalty import file... aborting import");
                        return;
                    }

                    if (fields[2].Substring(0, 5) == "01100" || fields[2].Contains("0000000"))
                        continue;
                    try
                    {

                        var expires = DateTime.Parse(fields[6]);
                        var when = DateTime.Parse(fields[5]);

                        var penaltyType = (Penalty.PenaltyType)Int32.Parse(fields[0]);
                        if (penaltyType == Penalty.PenaltyType.Ban)
                            expires = DateTime.MaxValue;

                        var penalty = new Penalty()
                        {
                            Type = penaltyType,
                            Expires = expires == DateTime.MinValue ? when : expires,
                            Punisher = new SharedLibrary.Database.Models.EFClient() { NetworkId = fields[3].ConvertLong() },
                            Offender = new SharedLibrary.Database.Models.EFClient() { NetworkId = fields[2].ConvertLong() },
                            Offense = fields[1].Replace("\"", "").Trim(),
                            Active = true,
                            When = when,
                        };


                        penalties.Add(penalty);
                    }

                    catch (Exception e)
                    {
                        manager.GetLogger().WriteVerbose($"Could not import penalty with line {line}");
                    }
                }
                //#if DO_IMPORT
                SharedLibrary.Database.Importer.ImportPenalties(penalties);
                manager.GetLogger().WriteVerbose($"Imported {penalties.Count} penalties");
                //#endif
            }
#endregion
#region CHATHISTORY

            if (File.Exists("import_chathistory.csv"))
            {
                var chatHistory = new List<EFClientMessage>();
                manager.GetLogger().WriteVerbose("Beginning import of existing messages");
                foreach (string line in File.ReadAllLines("import_chathistory.csv").Skip(1))
                {
                    string comma = Regex.Match(line, "\".*,.*\"").Value.Replace(",", "");
                    string[] fields = Regex.Replace(line, "\".*,.*\"", comma).Split(',');

                    fields.All(f =>
                                        {
                                            f = f.StripColors().Trim();
                                            return true;
                                        });

                    if (fields.Length != 4)
                    {
                        manager.GetLogger().WriteError("Invalid chat history import file... aborting import");
                        return;
                    }
                    try
                    {
                        int cId = Convert.ToInt32(fields[0]);
                        var linkedClient = oldClients[cId];

                        var newcl = cls.FirstOrDefault(c => c.NetworkId == linkedClient.NetworkId);
                        if (newcl == null)
                            newcl = cls.FirstOrDefault(c => c.Name == linkedClient.Name && c.IPAddress == linkedClient.IPAddress);
                        int newCId = newcl.ClientId;

                        var chatMessage = new EFClientMessage()
                        {
                            Active = true,
                            ClientId = newCId,
                            Message = fields[1],
                            TimeSent = DateTime.Parse(fields[3]),
                            ServerId = Math.Abs($"127.0.0.1:{Convert.ToInt32(fields[2]).ToString()}".GetHashCode())
                        };

                        chatHistory.Add(chatMessage);
                    }

                    catch (Exception e)
                    {
                        manager.GetLogger().WriteVerbose($"Could not import chatmessage with line {line}");
                    }
                }
                manager.GetLogger().WriteVerbose($"Read {chatHistory.Count} messages for import");
                SharedLibrary.Database.Importer.ImportSQLite(chatHistory);
            }
#endregion
#region STATS
            foreach (string file in Directory.GetFiles(Environment.CurrentDirectory))
            {
                if (Regex.Match(file, @"import_stats_[0-9]+.csv").Success)
                {
                    int port = Int32.Parse(Regex.Match(file, "[0-9]{5}").Value);
                    var stats = new List<EFClientStatistics>();
                    manager.GetLogger().WriteVerbose("Beginning import of existing client stats");

                    var lines = File.ReadAllLines(file).Skip(1);
                    foreach (string line in lines)
                    {
                        string[] fields = line.Split(',');

                        if (fields.Length != 9)
                        {
                            manager.GetLogger().WriteError("Invalid client import file... aborting import");
                            return;
                        }

                        try
                        {
                            if (fields[0].Substring(0, 5) == "01100")
                                continue;

                            long id = fields[0].ConvertLong();
                            var client = cls.Single(c => c.NetworkId == id);

                            var time = Convert.ToInt32(fields[8]);
                            double spm = time < 60 ? 0 : Math.Round(Convert.ToInt32(fields[1]) * 100.0 / time, 3);
                            if (spm > 1000)
                                spm = 0;

                            var st = new EFClientStatistics()
                            {
                                Active = true,
                                ClientId = client.ClientId,
                                ServerId = Math.Abs($"127.0.0.1:{port}".GetHashCode()),
                                Kills = Convert.ToInt32(fields[1]),
                                Deaths = Convert.ToInt32(fields[2]),
                                SPM = spm,
                                Skill = 0,
                                TimePlayed = time * 60
                            };
                           // client.TotalConnectionTime += time;
                            stats.Add(st);
                            stats = stats.AsEnumerable()
                                .GroupBy(c => new { c.ClientId })
                                .Select(c => c.FirstOrDefault()).ToList();

                            var cl = await manager.GetClientService().Get(st.ClientId);
                            cl.TotalConnectionTime += time * 60;
                            await manager.GetClientService().Update(cl);
                        }
                        catch (Exception e)
                        {
                            continue;
                        }

                    }


                    manager.GetLogger().WriteVerbose($"Read {stats.Count} clients stats for import");

                    try
                    {
                        SharedLibrary.Database.Importer.ImportSQLite(stats);
                    }

                    catch (Exception e)
                    {
                        manager.GetLogger().WriteError("Saving imported stats failed");
                    }
                }
            }
#endregion
        }

        public async Task OnTickAsync(Server S)
        {
            return;
            if ((DateTime.Now - Interval).TotalSeconds > 1)
            {
                var rand = new Random();
                int index = rand.Next(0, 17);
                var p = new Player()
                {
                    Name = $"Test_{index}",
                    NetworkId = (long)$"_test_{index}".GetHashCode(),
                    ClientNumber = index,
                    Ping = 1,
                    IPAddress = $"127.0.0.{index}".ConvertToIP()
                };

                if (S.Players.ElementAt(index) != null)
                    await S.RemovePlayer(index);
                // await S.AddPlayer(p);


                Interval = DateTime.Now;
                if (S.ClientNum > 0)
                {
                    var victimPlayer = S.Players.Where(pl => pl != null).ToList()[rand.Next(0, S.ClientNum - 1)];
                    var attackerPlayer = S.Players.Where(pl => pl != null).ToList()[rand.Next(0, S.ClientNum - 1)];

                    await S.ExecuteEvent(new Event(Event.GType.Say, $"test_{attackerPlayer.ClientNumber}", victimPlayer, attackerPlayer, S));

                    string[] eventLine = null;

                    for (int i = 0; i < 1; i++)
                    {
                        if (S.GameName == Server.Game.IW4)
                        {

                            // attackerID ; victimID ; attackerOrigin ; victimOrigin ; Damage ; Weapon ; hitLocation ; meansOfDeath
                            var minimapInfo = StatsPlugin.MinimapConfig.IW4Minimaps().MapInfo.FirstOrDefault(m => m.MapName == S.CurrentMap.Name);
                            if (minimapInfo == null)
                                return;
                            eventLine = new string[]
                            {
                            "ScriptKill",
                            attackerPlayer.NetworkId.ToString(),
                            victimPlayer.NetworkId.ToString(),
                            new Vector3(rand.Next(minimapInfo.MaxRight, minimapInfo.MaxLeft), rand.Next(minimapInfo.MaxBottom, minimapInfo.MaxTop), rand.Next(0, 100)).ToString(),
                            new Vector3(rand.Next(minimapInfo.MaxRight, minimapInfo.MaxLeft), rand.Next(minimapInfo.MaxBottom, minimapInfo.MaxTop), rand.Next(0, 100)).ToString(),
                            rand.Next(50, 105).ToString(),
                            ((StatsPlugin.IW4Info.WeaponName)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.WeaponName)).Length - 1)).ToString(),
                            ((StatsPlugin.IW4Info.HitLocation)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.HitLocation)).Length - 1)).ToString(),
                            ((StatsPlugin.IW4Info.MeansOfDeath)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.MeansOfDeath)).Length - 1)).ToString()
                            };

                        }
                        else
                        {
                            eventLine = new string[]
                           {
                            "K",
                            victimPlayer.NetworkId.ToString(),
                            victimPlayer.ClientNumber.ToString(),
                            rand.Next(0, 1) == 0  ? "allies" : "axis",
                            victimPlayer.Name,
                            attackerPlayer.NetworkId.ToString(),
                            attackerPlayer.ClientNumber.ToString(),
                            rand.Next(0, 1) == 0  ? "allies" : "axis",
                            attackerPlayer.Name.ToString(),
                            ((StatsPlugin.IW4Info.WeaponName)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.WeaponName)).Length - 1)).ToString(), // Weapon
                            rand.Next(50, 105).ToString(),                                  // Damage
                            ((StatsPlugin.IW4Info.MeansOfDeath)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.MeansOfDeath)).Length - 1)).ToString(),  // Means of Death
                            ((StatsPlugin.IW4Info.HitLocation)rand.Next(0, Enum.GetValues(typeof(StatsPlugin.IW4Info.HitLocation)).Length - 1)).ToString(),                 // Hit Location
                           };
                        }

                        var _event = Event.ParseEventString(eventLine, S);
                        await S.ExecuteEvent(_event);
                    }
                }
            }

        }

        public async Task OnUnloadAsync()
        {

        }
    }
}
#endif