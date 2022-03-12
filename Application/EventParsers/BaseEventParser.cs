using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Data.Models;
using Microsoft.Extensions.Logging;
using static SharedLibraryCore.Server;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.EventParsers
{
    public class BaseEventParser : IEventParser
    {
        private readonly Dictionary<string, (string, Func<string, IEventParserConfiguration, GameEvent, GameEvent>)> _customEventRegistrations;
        private readonly ILogger _logger;
        private readonly ApplicationConfiguration _appConfig;
        private readonly Dictionary<ParserRegex, GameEvent.EventType> _regexMap;
        private readonly Dictionary<string, GameEvent.EventType> _eventTypeMap;

        public BaseEventParser(IParserRegexFactory parserRegexFactory, ILogger logger, ApplicationConfiguration appConfig)
        {
            _customEventRegistrations = new Dictionary<string, (string, Func<string, IEventParserConfiguration, GameEvent, GameEvent>)>();
            _logger = logger;
            _appConfig = appConfig;

            Configuration = new DynamicEventParserConfiguration(parserRegexFactory)
            {
                GameDirectory = "main",
            };

            Configuration.Say.Pattern = @"^(say|sayteam);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+|0);([0-9]+);([^;]*);(.*)$";
            Configuration.Say.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Say.AddMapping(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Say.AddMapping(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Say.AddMapping(ParserRegex.GroupType.OriginName, 4);
            Configuration.Say.AddMapping(ParserRegex.GroupType.Message, 5);

            Configuration.Quit.Pattern = @"^(Q);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+|0);([0-9]+);(.*)$";
            Configuration.Quit.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Quit.AddMapping(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Quit.AddMapping(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Quit.AddMapping(ParserRegex.GroupType.OriginName, 4);

            Configuration.Join.Pattern = @"^(J);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+|0);([0-9]+);(.*)$";
            Configuration.Join.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Join.AddMapping(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Join.AddMapping(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Join.AddMapping(ParserRegex.GroupType.OriginName, 4);
            
            Configuration.JoinTeam.Pattern = @"^(JT);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+|0);([0-9]+);(\w+);(.+)$";
            Configuration.JoinTeam.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.JoinTeam.AddMapping(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.JoinTeam.AddMapping(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.JoinTeam.AddMapping(ParserRegex.GroupType.OriginTeam, 4);
            Configuration.JoinTeam.AddMapping(ParserRegex.GroupType.OriginName, 5);

            Configuration.Damage.Pattern = @"^(D);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+|0);(-?[0-9]+);(axis|allies|world|none)?;([^;]{1,32});(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+|0)?;(-?[0-9]+);(axis|allies|world|none)?;([^;]{1,32})?;((?:[0-9]+|[a-z]+|_|\+)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
            Configuration.Damage.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.TargetNetworkId, 2);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.TargetClientNumber, 3);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.TargetTeam, 4);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.TargetName, 5);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.OriginNetworkId, 6);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.OriginClientNumber, 7);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.OriginTeam, 8);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.OriginName, 9);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.Weapon, 10);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.Damage, 11);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.MeansOfDeath, 12);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.HitLocation, 13);

            Configuration.Kill.Pattern = @"^(K);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+|0);(-?[0-9]+);(axis|allies|world|none)?;([^;]{1,32});(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+|0)?;(-?[0-9]+);(axis|allies|world|none)?;([^;]{1,32})?;((?:[0-9]+|[a-z]+|_|\+)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
            Configuration.Kill.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetNetworkId, 2);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetClientNumber, 3);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetTeam, 4);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetName, 5);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginNetworkId, 6);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginClientNumber, 7);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginTeam, 8);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginName, 9);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.Weapon, 10);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.Damage, 11);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.MeansOfDeath, 12);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.HitLocation, 13);

            Configuration.MapChange.Pattern = @".*InitGame.*";
            Configuration.MapEnd.Pattern = @".*(?:ExitLevel|ShutdownGame).*";

            Configuration.Time.Pattern = @"^ *(([0-9]+):([0-9]+) |^[0-9]+ )";

            _regexMap = new Dictionary<ParserRegex, GameEvent.EventType>
            {
                {Configuration.Say, GameEvent.EventType.Say},
                {Configuration.Kill, GameEvent.EventType.Kill},
                {Configuration.MapChange, GameEvent.EventType.MapChange},
                {Configuration.MapEnd, GameEvent.EventType.MapEnd}
            };
            
            _eventTypeMap = new Dictionary<string, GameEvent.EventType>
            {
                {"say", GameEvent.EventType.Say},
                {"sayteam", GameEvent.EventType.Say},
                {"K", GameEvent.EventType.Kill},
                {"D", GameEvent.EventType.Damage},
                {"J", GameEvent.EventType.PreConnect},
                {"JT", GameEvent.EventType.JoinTeam },
                {"Q", GameEvent.EventType.PreDisconnect},
            };
        }

        public IEventParserConfiguration Configuration { get; set; }

        public string Version { get; set; } = "CoD";

        public Game GameName { get; set; } = Game.COD;

        public string URLProtocolFormat { get; set; } = "CoD://{{ip}}:{{port}}";

        public string Name { get; set; } = "Call of Duty";

        private (GameEvent.EventType type, string eventKey) GetEventTypeFromLine(string logLine)
        {
            var lineSplit = logLine.Split(';');
            if (lineSplit.Length > 1)
            {
                var type = lineSplit[0];
                return _eventTypeMap.ContainsKey(type) ? (_eventTypeMap[type], type): (GameEvent.EventType.Unknown, lineSplit[0]);
            }

            foreach (var (key, value) in _regexMap)
            {
                var result = key.PatternMatcher.Match(logLine);
                if (result.Success)
                {
                    return (value, null);
                }
            }

            return (GameEvent.EventType.Unknown, null);
        }
        

        public virtual GameEvent GenerateGameEvent(string logLine)
        {
            var timeMatch = Configuration.Time.PatternMatcher.Match(logLine);
            var gameTime = 0L;

            if (timeMatch.Success)
            {
                if (timeMatch.Values[0].Contains(":"))
                {
                    gameTime = timeMatch
                        .Values
                        .Skip(2)
                        // this converts the timestamp into seconds passed
                        .Select((value, index) => long.Parse(value.ToString()) * (index == 0 ? 60 : 1))
                        .Sum();
         
                }
                else
                {
                    gameTime = long.Parse(timeMatch.Values[0]);
                }
                
                // we want to strip the time from the log line
                logLine = logLine.Substring(timeMatch.Values.First().Length).Trim();
            }

            var eventParseResult = GetEventTypeFromLine(logLine);
            var eventType = eventParseResult.type;
            
            _logger.LogDebug(logLine);

            if (eventType == GameEvent.EventType.Say)
            {
                var matchResult = Configuration.Say.PatternMatcher.Match(logLine);

                if (matchResult.Success)
                {
                    var message = matchResult.Values[Configuration.Say.GroupMapping[ParserRegex.GroupType.Message]]
                        .Replace("\x15", "")
                        .Trim();

                    if (message.Length > 0)
                    {
                        var originIdString = matchResult.Values[Configuration.Say.GroupMapping[ParserRegex.GroupType.OriginNetworkId]];
                        var originName = matchResult.Values[Configuration.Say.GroupMapping[ParserRegex.GroupType.OriginName]];

                        var originId = originIdString.IsBotGuid() ?
                            originName.GenerateGuidFromString() :
                            originIdString.ConvertGuidToLong(Configuration.GuidNumberStyle);

                        var clientNumber = int.Parse(matchResult.Values[Configuration.Say.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]);

                        if (message.StartsWith(_appConfig.CommandPrefix) || message.StartsWith(_appConfig.BroadcastCommandPrefix))
                        {
                            return new GameEvent()
                            {
                                Type = GameEvent.EventType.Command,
                                Data = message,
                                Origin = new EFClient() { NetworkId = originId, ClientNumber = clientNumber },
                                Message = message,
                                Extra = logLine,
                                RequiredEntity = GameEvent.EventRequiredEntity.Origin,
                                GameTime = gameTime,
                                Source = GameEvent.EventSource.Log
                            };
                        }

                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Say,
                            Data = message,
                            Origin = new EFClient() { NetworkId = originId, ClientNumber = clientNumber },
                            Message = message,
                            Extra = logLine,
                            RequiredEntity = GameEvent.EventRequiredEntity.Origin,
                            GameTime = gameTime,
                            Source = GameEvent.EventSource.Log
                        };
                    }
                }
            }

            if (eventType == GameEvent.EventType.Kill)
            {
                var match = Configuration.Kill.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    var originIdString = match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.OriginNetworkId]];
                    var targetIdString = match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.TargetNetworkId]];
                    var originName = match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.OriginName]];
                    var targetName = match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.TargetName]];

                    var originId = originIdString.IsBotGuid() ?
                        originName.GenerateGuidFromString() :
                        originIdString.ConvertGuidToLong(Configuration.GuidNumberStyle, Utilities.WORLD_ID);
                    var targetId = targetIdString.IsBotGuid() ?
                        targetName.GenerateGuidFromString() :
                        targetIdString.ConvertGuidToLong(Configuration.GuidNumberStyle, Utilities.WORLD_ID);

                    var originClientNumber = int.Parse(match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]);
                    var targetClientNumber = int.Parse(match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.TargetClientNumber]]);

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Kill,
                        Data = logLine,
                        Origin = new EFClient() { NetworkId = originId, ClientNumber = originClientNumber },
                        Target = new EFClient() { NetworkId = targetId, ClientNumber = targetClientNumber },
                        RequiredEntity = GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target,
                        GameTime = gameTime,
                        Source = GameEvent.EventSource.Log
                    };
                }
            }

            if (eventType == GameEvent.EventType.Damage)
            {
                var match = Configuration.Damage.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    var originIdString = match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.OriginNetworkId]];
                    var targetIdString = match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.TargetNetworkId]];
                    var originName = match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.OriginName]];
                    var targetName = match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.TargetName]];

                    var originId = originIdString.IsBotGuid() ?
                        originName.GenerateGuidFromString() :
                        originIdString.ConvertGuidToLong(Configuration.GuidNumberStyle, Utilities.WORLD_ID);
                    var targetId = targetIdString.IsBotGuid() ?
                        targetName.GenerateGuidFromString() :
                        targetIdString.ConvertGuidToLong(Configuration.GuidNumberStyle, Utilities.WORLD_ID);

                    var originClientNumber = int.Parse(match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]);
                    var targetClientNumber = int.Parse(match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.TargetClientNumber]]);

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Damage,
                        Data = logLine,
                        Origin = new EFClient() { NetworkId = originId, ClientNumber = originClientNumber },
                        Target = new EFClient() { NetworkId = targetId, ClientNumber = targetClientNumber },
                        RequiredEntity = GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target,
                        GameTime = gameTime,
                        Source = GameEvent.EventSource.Log
                    };
                }
            }

            if (eventType == GameEvent.EventType.PreConnect)
            {
                var match = Configuration.Join.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    var originIdString = match.Values[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginNetworkId]];
                    var originName = match.Values[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginName]];

                    var networkId = originIdString.IsBotGuid() ?
                        originName.GenerateGuidFromString() :
                        originIdString.ConvertGuidToLong(Configuration.GuidNumberStyle);

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.PreConnect,
                        Data = logLine,
                        Origin = new EFClient()
                        {
                            CurrentAlias = new EFAlias()
                            {
                                Name = match.Values[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginName]].TrimNewLine(),
                            },
                            NetworkId = networkId,
                            ClientNumber = Convert.ToInt32(match.Values[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]),
                            State = EFClient.ClientState.Connecting,
                        },
                        Extra = originIdString,
                        RequiredEntity = GameEvent.EventRequiredEntity.None,
                        IsBlocking = true,
                        GameTime = gameTime,
                        Source = GameEvent.EventSource.Log
                    };
                }
            }

            if (eventType == GameEvent.EventType.JoinTeam)
            {
                var match = Configuration.JoinTeam.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    var originIdString = match.Values[Configuration.JoinTeam.GroupMapping[ParserRegex.GroupType.OriginNetworkId]];
                    var originName = match.Values[Configuration.JoinTeam.GroupMapping[ParserRegex.GroupType.OriginName]];
                    var team = match.Values[Configuration.JoinTeam.GroupMapping[ParserRegex.GroupType.OriginTeam]];

                    var networkId = originIdString.IsBotGuid() ?
                        originName.GenerateGuidFromString() :
                        originIdString.ConvertGuidToLong(Configuration.GuidNumberStyle);
                    
                    return new GameEvent
                    {
                        Type = GameEvent.EventType.JoinTeam,
                        Data = logLine,
                        Origin = new EFClient
                        {
                            CurrentAlias = new EFAlias
                            {
                                Name = match.Values[Configuration.JoinTeam.GroupMapping[ParserRegex.GroupType.OriginName]].TrimNewLine(),
                            },
                            NetworkId = networkId,
                            ClientNumber = Convert.ToInt32(match.Values[Configuration.JoinTeam.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]),
                            State = EFClient.ClientState.Connected,
                        },
                        Extra = team,
                        RequiredEntity = GameEvent.EventRequiredEntity.None,
                        GameTime = gameTime,
                        Source = GameEvent.EventSource.Log
                    };
                }
            }

            if (eventType == GameEvent.EventType.PreDisconnect)
            {
                var match = Configuration.Quit.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    var originIdString = match.Values[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginNetworkId]];
                    var originName = match.Values[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginName]];

                    var networkId = originIdString.IsBotGuid() ?
                        originName.GenerateGuidFromString() :
                        originIdString.ConvertGuidToLong(Configuration.GuidNumberStyle);

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.PreDisconnect,
                        Data = logLine,
                        Origin = new EFClient()
                        {
                            CurrentAlias = new EFAlias()
                            {
                                Name = match.Values[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginName]].TrimNewLine()
                            },
                            NetworkId = networkId,
                            ClientNumber = Convert.ToInt32(match.Values[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]),
                            State = EFClient.ClientState.Disconnecting
                        },
                        RequiredEntity = GameEvent.EventRequiredEntity.None,
                        IsBlocking = true,
                        GameTime = gameTime,
                        Source = GameEvent.EventSource.Log
                    };
                }
            }

            if (eventType == GameEvent.EventType.MapEnd)
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapEnd,
                    Data = logLine,
                    Origin = Utilities.IW4MAdminClient(),
                    Target = Utilities.IW4MAdminClient(),
                    RequiredEntity = GameEvent.EventRequiredEntity.None,
                    GameTime = gameTime,
                    Source = GameEvent.EventSource.Log
                };
            }

            if (eventType == GameEvent.EventType.MapChange)
            {
                var dump = logLine.Replace("InitGame: ", "");

                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapChange,
                    Data = logLine,
                    Origin = Utilities.IW4MAdminClient(),
                    Target = Utilities.IW4MAdminClient(),
                    Extra = dump.DictionaryFromKeyValue(),
                    RequiredEntity = GameEvent.EventRequiredEntity.None,
                    GameTime = gameTime,
                    Source = GameEvent.EventSource.Log
                };
            }

            if (eventParseResult.eventKey == null || !_customEventRegistrations.ContainsKey(eventParseResult.eventKey))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.Unknown,
                    Data = logLine,
                    Origin = Utilities.IW4MAdminClient(),
                    Target = Utilities.IW4MAdminClient(),
                    RequiredEntity = GameEvent.EventRequiredEntity.None,
                    GameTime = gameTime,
                    Source = GameEvent.EventSource.Log
                };
            }

            var eventModifier = _customEventRegistrations[eventParseResult.eventKey];

            try
            {
                return eventModifier.Item2(logLine, Configuration, new GameEvent()
                {
                    Type = GameEvent.EventType.Other,
                    Data = logLine,
                    Subtype = eventModifier.Item1,
                    GameTime = gameTime,
                    Source = GameEvent.EventSource.Log
                });
            }

            catch (Exception e)
            {
                _logger.LogError(e, "Could not handle custom event generation");
            }

            return new GameEvent()
            {
                Type = GameEvent.EventType.Unknown,
                Data = logLine,
                Origin = Utilities.IW4MAdminClient(),
                Target = Utilities.IW4MAdminClient(),
                RequiredEntity = GameEvent.EventRequiredEntity.None,
                GameTime = gameTime,
                Source = GameEvent.EventSource.Log
            };
        }

        /// <inheritdoc/>
        public void RegisterCustomEvent(string eventSubtype, string eventTriggerValue, Func<string, IEventParserConfiguration, GameEvent, GameEvent> eventModifier)
        {
            if (string.IsNullOrWhiteSpace(eventSubtype))
            {
                throw new ArgumentException("Event subtype cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(eventTriggerValue))
            {
                throw new ArgumentException("Event trigger value cannot be empty");
            }

            if (eventModifier == null)
            {
                throw new ArgumentException("Event modifier must be specified");
            }

            if (_customEventRegistrations.ContainsKey(eventTriggerValue))
            {
                throw new ArgumentException($"Event trigger value '{eventTriggerValue}' is already registered");
            }

            _customEventRegistrations.Add(eventTriggerValue, (eventSubtype, eventModifier));
        }
    }
}
