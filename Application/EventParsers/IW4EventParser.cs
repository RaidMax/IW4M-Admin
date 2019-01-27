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
        private IEventParserConfiguration _configuration;

        public IW4EventParser()
        {
            _configuration = new DynamicEventParserConfiguration()
            {
                GameDirectory = "userraw",
            };

            _configuration.Say.Pattern = @"^(say|sayteam);(.{1,32});([0-9]+)(.*);(.*)$";
            _configuration.Say.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            _configuration.Say.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 2);
            _configuration.Say.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 3);
            _configuration.Say.GroupMapping.Add(ParserRegex.GroupType.OriginName, 4);
            _configuration.Say.GroupMapping.Add(ParserRegex.GroupType.Message, 5);

            _configuration.Quit.Pattern = @"^(Q);(.{16,32}|bot[0-9]+);([0-9]+);(.*)$";
            _configuration.Quit.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            _configuration.Quit.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 2);
            _configuration.Quit.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 3);
            _configuration.Quit.GroupMapping.Add(ParserRegex.GroupType.OriginName, 4);

            _configuration.Join.Pattern = @"^(J);(.{16,32}|bot[0-9]+);([0-9]+);(.*)$";
            _configuration.Join.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            _configuration.Join.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 2);
            _configuration.Join.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 3);
            _configuration.Join.GroupMapping.Add(ParserRegex.GroupType.OriginName, 4);

            _configuration.Damage.Pattern = @"^(D);([A-Fa-f0-9_]{16,32}|bot[0-9]+);(-?[0-9]+);(axis|allies|world);(.{1,24});([A-Fa-f0-9_]{16,32}|bot[0-9]+)?;-?([0-9]+);(axis|allies|world);(.{1,24})?;((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.TargetNetworkId, 2);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.TargetClientNumber, 3);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.TargetTeam, 4);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.TargetName, 5);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 6);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 7);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.OriginTeam, 8);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.OriginName, 9);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.Weapon, 10);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.Damage, 11);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.MeansOfDeath, 12);
            _configuration.Damage.GroupMapping.Add(ParserRegex.GroupType.HitLocation, 13);

            _configuration.Kill.Pattern = @"^(K);([A-Fa-f0-9_]{16,32}|bot[0-9]+);(-?[0-9]+);(axis|allies|world);(.{1,24});([A-Fa-f0-9_]{16,32}|bot[0-9]+)?;-?([0-9]+);(axis|allies|world);(.{1,24})?;((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.EventType, 1);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.TargetNetworkId, 2);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.TargetClientNumber, 3);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.TargetTeam, 4);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.TargetName, 5);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.OriginNetworkId, 6);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.OriginClientNumber, 7);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.OriginTeam, 8);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.OriginName, 9);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.Weapon, 10);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.Damage, 11);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.MeansOfDeath, 12);
            _configuration.Kill.GroupMapping.Add(ParserRegex.GroupType.HitLocation, 13);
        }

        public IEventParserConfiguration Configuration { get => _configuration; set => _configuration = value; }

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
                var matchResult = Regex.Match(logLine, _configuration.Say.Pattern);

                if (matchResult.Success)
                {
                    string message = matchResult
                        .Groups[_configuration.Say.GroupMapping[ParserRegex.GroupType.Message]]
                        .ToString()
                        .Replace("\x15", "")
                        .Trim();

                    var origin = server.GetClientsAsList()
                        .First(c => c.NetworkId == matchResult.Groups[_configuration.Say.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong());

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
                    var match = Regex.Match(logLine, _configuration.Kill.Pattern);

                    if (match.Success)
                    {
                        var origin = server.GetClientsAsList()
                                .First(c => c.NetworkId == match.Groups[_configuration.Kill.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong());
                        var target = server.GetClientsAsList()
                            .First(c => c.NetworkId == match.Groups[_configuration.Kill.GroupMapping[ParserRegex.GroupType.TargetNetworkId]].ToString().ConvertLong());


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
               // if (!server.CustomCallback)
                {
                    var regexMatch = Regex.Match(logLine, _configuration.Damage.Pattern);

                    if (regexMatch.Success)
                    {
                        var origin = server.GetClientsAsList()
                            .First(c => c.NetworkId == regexMatch.Groups[_configuration.Damage.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong());
                        var target = server.GetClientsAsList()
                            .First(c => c.NetworkId == regexMatch.Groups[_configuration.Damage.GroupMapping[ParserRegex.GroupType.TargetNetworkId]].ToString().ConvertLong());

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
                var regexMatch = Regex.Match(logLine, _configuration.Join.Pattern);
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
                                Name = regexMatch.Groups[_configuration.Join.GroupMapping[ParserRegex.GroupType.OriginName]].ToString().StripColors(),
                            },
                            NetworkId = regexMatch.Groups[_configuration.Join.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong(),
                            ClientNumber = Convert.ToInt32(regexMatch.Groups[_configuration.Join.GroupMapping[ParserRegex.GroupType.OriginClientNumber]].ToString()),
                            State = EFClient.ClientState.Connecting,
                            CurrentServer = server
                        }
                    };
                }
            }

            if (eventType == "Q")
            {
                var regexMatch = Regex.Match(logLine, _configuration.Quit.Pattern);
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
                                Name = regexMatch.Groups[_configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginName]].ToString().StripColors()
                            },
                            NetworkId = regexMatch.Groups[_configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertLong(),
                            ClientNumber = Convert.ToInt32(regexMatch.Groups[_configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginClientNumber]].ToString()),
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
