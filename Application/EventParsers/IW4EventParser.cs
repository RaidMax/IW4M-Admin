using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace IW4MAdmin.Application.EventParsers
{
    class IW4EventParser : IEventParser
    {
        public IW4EventParser()
        {
            Configuration = new DynamicEventParserConfiguration()
            {
                GameDirectory = "userraw",
            };

            Configuration.Say.Pattern = @"^(say|sayteam);(.{1,32});([0-9]+)(.*);(.*)$";
            Configuration.Say.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            Configuration.Say.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Say.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Say.GroupMapping.Add(ParserRegex.GroupType.OriginName, 4);
            Configuration.Say.GroupMapping.Add(ParserRegex.GroupType.Message, 5);

            Configuration.Quit.Pattern = @"^(Q);(.{16,32}|bot[0-9]+);([0-9]+);(.*)$";
            Configuration.Quit.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            Configuration.Quit.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Quit.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Quit.GroupMapping.Add(ParserRegex.GroupType.OriginName, 4);

            Configuration.Join.Pattern = @"^(J);(.{16,32}|bot[0-9]+);([0-9]+);(.*)$";
            Configuration.Join.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            Configuration.Join.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Join.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Join.GroupMapping.Add(ParserRegex.GroupType.OriginName, 4);

            Configuration.Damage.Pattern = @"^(D);([A-Fa-f0-9_]{16,32}|bot[0-9]+);(-?[0-9]+);(axis|allies|world);(.{1,24});([A-Fa-f0-9_]{16,32}|bot[0-9]+)?;-?([0-9]+);(axis|allies|world);(.{1,24})?;((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.TargetNetworkId, 2);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.TargetClientNumber, 3);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.TargetTeam, 4);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.TargetName, 5);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 6);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 7);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.OriginTeam, 8);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.OriginName, 9);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.Weapon, 10);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.Damage, 11);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.MeansOfDeath, 12);
            Configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.HitLocation, 13);

            Configuration.Kill.Pattern = @"^(K);([A-Fa-f0-9_]{16,32}|bot[0-9]+);(-?[0-9]+);(axis|allies|world);(.{1,24});([A-Fa-f0-9_]{16,32}|bot[0-9]+)?;-?([0-9]+);(axis|allies|world);(.{1,24})?;((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.TargetNetworkId, 2);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.TargetClientNumber, 3);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.TargetTeam, 4);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.TargetName, 5);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 6);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 7);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.OriginTeam, 8);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.OriginName, 9);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.Weapon, 10);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.Damage, 11);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.MeansOfDeath, 12);
            Configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.HitLocation, 13);
        }

        public IEventParserConfiguration Configuration { get; set; }

        public virtual GameEvent GetEvent(Server server, string logLine)
        {
            logLine = Regex.Replace(logLine, @"([0-9]+:[0-9]+ |^[0-9]+ )", "").Trim();
            string[] lineSplit = logLine.Split(';');
            string eventType = lineSplit[0];

            if (eventType == "JoinTeam")
            {
                var origin = server.GetClientsAsList()
                    .FirstOrDefault(c => c.NetworkId == lineSplit[1].ConvertLong());

                return new GameEvent()
                {
                    Type = GameEvent.EventType.JoinTeam,
                    Data = eventType,
                    Origin = origin,
                    Owner = server
                };
            }

            if (eventType == "say" || eventType == "sayteam")
            {
                var matchResult = Regex.Match(logLine, Configuration.Say.Pattern);

                if (matchResult.Success)
                {
                    string message = matchResult
                        .Groups[Configuration.Say.GroupMapping[ParserRegex.GroupType.Message]]
                        .ToString()
                        .Replace("\x15", "")
                        .Trim();

                    var origin = server.GetClientsAsList()
                        .First(c => c.NetworkId == matchResult.Groups[Configuration.Say.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong());

                    if (message[0] == '!' || message[0] == '@')
                    {
                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Command,
                            Data = message,
                            Origin = origin,
                            Owner = server,
                            Message = message
                        };
                    }

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Say,
                        Data = message,
                        Origin = origin,
                        Owner = server,
                        Message = message
                    };
                }
            }

            if (eventType == "K")
            {
                if (!server.CustomCallback)
                {
                    var match = Regex.Match(logLine, Configuration.Kill.Pattern);

                    if (match.Success)
                    {
                        var origin = server.GetClientsAsList()
                                .First(c => c.NetworkId == match.Groups[Configuration.Kill.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong());
                        var target = server.GetClientsAsList()
                            .First(c => c.NetworkId == match.Groups[Configuration.Kill.GroupMapping[ParserRegex.GroupType.TargetNetworkId]].ToString().ConvertLong());


                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Kill,
                            Data = logLine,
                            Origin = origin,
                            Target = target,
                            Owner = server
                        };
                    }
                }
            }

            if (eventType == "ScriptKill")
            {
                var origin = server.GetClientsAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong());
                var target = server.GetClientsAsList().First(c => c.NetworkId == lineSplit[2].ConvertLong());
                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptKill,
                    Data = logLine,
                    Origin = origin,
                    Target = target,
                    Owner = server
                };
            }

            if (eventType == "ScriptDamage")
            {
                var origin = server.GetClientsAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong());
                var target = server.GetClientsAsList().First(c => c.NetworkId == lineSplit[2].ConvertLong());

                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptDamage,
                    Data = logLine,
                    Origin = origin,
                    Target = target,
                    Owner = server
                };
            }

            // damage
            if (eventType == "D")
            {
                if (!server.CustomCallback)
                {
                    var regexMatch = Regex.Match(logLine, Configuration.Damage.Pattern);

                    if (regexMatch.Success)
                    {
                        var origin = server.GetClientsAsList()
                            .First(c => c.NetworkId == regexMatch.Groups[Configuration.Damage.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong());
                        var target = server.GetClientsAsList()
                            .First(c => c.NetworkId == regexMatch.Groups[Configuration.Damage.GroupMapping[ParserRegex.GroupType.TargetNetworkId]].ToString().ConvertLong());

                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Damage,
                            Data = eventType,
                            Origin = origin,
                            Target = target,
                            Owner = server
                        };
                    }
                }
            }

            // join
            if (eventType == "J")
            {
                var regexMatch = Regex.Match(logLine, Configuration.Join.Pattern);
                if (regexMatch.Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.PreConnect,
                        Data = logLine,
                        Owner = server,
                        Origin = new EFClient()
                        {
                            CurrentAlias = new EFAlias()
                            {
                                Active = false,
                                Name = regexMatch.Groups[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginName]].ToString().StripColors(),
                            },
                            NetworkId = regexMatch.Groups[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong(),
                            ClientNumber = Convert.ToInt32(regexMatch.Groups[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginClientNumber]].ToString()),
                            State = EFClient.ClientState.Connecting,
                            CurrentServer = server
                        }
                    };
                }
            }

            if (eventType == "Q")
            {
                var regexMatch = Regex.Match(logLine, Configuration.Quit.Pattern);
                if (regexMatch.Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.PreDisconnect,
                        Data = logLine,
                        Owner = server,
                        Origin = new EFClient()
                        {
                            CurrentAlias = new EFAlias()
                            {
                                Active = false,
                                Name = regexMatch.Groups[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginName]].ToString().StripColors()
                            },
                            NetworkId = regexMatch.Groups[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong(),
                            ClientNumber = Convert.ToInt32(regexMatch.Groups[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginClientNumber]].ToString()),
                            State = EFClient.ClientState.Disconnecting
                        }
                    };
                }
            }

            if (eventType.Contains("ExitLevel"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapEnd,
                    Data = lineSplit[0],
                    Origin = Utilities.IW4MAdminClient(server),
                    Target = Utilities.IW4MAdminClient(server),
                    Owner = server
                };
            }

            if (eventType.Contains("InitGame"))
            {
                string dump = eventType.Replace("InitGame: ", "");

                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapChange,
                    Data = lineSplit[0],
                    Origin = Utilities.IW4MAdminClient(server),
                    Target = Utilities.IW4MAdminClient(server),
                    Owner = server,
                    Extra = dump.DictionaryFromKeyValue()
                };
            }

            return new GameEvent()
            {
                Type = GameEvent.EventType.Unknown,
                Origin = Utilities.IW4MAdminClient(server),
                Target = Utilities.IW4MAdminClient(server),
                Owner = server
            };
        }
    }
}
