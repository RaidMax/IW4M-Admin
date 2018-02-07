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
            Interval = DateTime.Now;
            #region CLIENTS
            if (File.Exists("import_clients.csv"))
            {
                var clients = new List<Player>();
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

                    if (fields[1].Contains("0110") || fields[0] == string.Empty || fields[1] == string.Empty || fields[6] == string.Empty)
                        continue;

                    if (!Regex.Match(fields[6], @"^\d+\.\d+\.\d+.\d+$").Success)
                        continue;

                    var client = new Player()
                    {
                        Name = fields[0],
                        NetworkId = fields[1],
                        IPAddress = fields[6],
                        Level = (Player.Permission)Convert.ToInt32(fields[3]),
                        Connections = Convert.ToInt32(fields[5]),
                        LastConnection = DateTime.Parse(fields[7]),
                    };

                    clients.Add(client);
                }

                clients = clients
                    .GroupBy(c => c.NetworkId, (key, c) => c.FirstOrDefault())
                    .ToList();

                clients = clients
                    .GroupBy(c => new { c.Name, c.IPAddress })
                    .Select(c => c.FirstOrDefault())
                    .ToList();

                manager.GetLogger().WriteVerbose($"Read {clients.Count} clients for import");

                try
                {
                    SharedLibrary.Database.Importer.ImportClients(clients);
                }

                catch(Exception e)
                {
                    manager.GetLogger().WriteError("Saving imported clients failed");
                }
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

                    if (fields[2].Contains("0110") || fields[2].Contains("0000000") || fields.Any(p => p == string.Empty))
                        continue;
                    try
                    {
                        
                        var expires = DateTime.Parse(fields[6]);
                        var when = DateTime.Parse(fields[5]);

                        var penalty = new Penalty()
                        {
                            Type = (Penalty.PenaltyType)Int32.Parse(fields[0]),
                            Expires = expires == DateTime.MinValue ? when : expires, 
                            Punisher = new SharedLibrary.Database.Models.EFClient() {  NetworkId = fields[3]},
                            Offender = new SharedLibrary.Database.Models.EFClient() {  NetworkId = fields[2]},
                            Offense = fields[1],
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
                SharedLibrary.Database.Importer.ImportPenalties(penalties);
                manager.GetLogger().WriteVerbose($"Imported {penalties.Count} penalties");
            }
            #endregion
        }

        public async Task OnTickAsync(Server S)
        {

            if ((DateTime.Now - Interval).TotalSeconds > 1)
            {
                var rand = new Random();
                int index = rand.Next(0, 17);
                var p = new Player()
                {
                    Name = $"Test_{index}",
                    NetworkId = $"_test_{index}",
                    ClientNumber = index,
                    Ping = 1,
                    IPAddress = $"127.0.0.{index}"
                };

                if (S.Players.ElementAt(index) != null)
                    await S.RemovePlayer(index);
               // await S.AddPlayer(p);
                

                Interval = DateTime.Now;
                if (S.ClientNum > 0)
                {

                    //"K;26d2f66b95184934;1;allies;egor;5c56fef676b3818d;0;axis;1_din;m21_heartbeat_mp;98;MOD_RIFLE_BULLET;torso_lower";
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
                            attackerPlayer.NetworkId,
                            victimPlayer.NetworkId,
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
                            victimPlayer.NetworkId,
                            victimPlayer.ClientNumber.ToString(),
                            rand.Next(0, 1) == 0  ? "allies" : "axis",
                            victimPlayer.Name,
                            attackerPlayer.NetworkId,
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