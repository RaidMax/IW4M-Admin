using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore.Localization;

namespace SharedLibraryCore.Database.Models
{
    public class EFClient : Data.Models.Client.EFClient
    {
        public enum ClientState
        {
            /// <summary>
            ///     default client state
            /// </summary>
            Unknown,

            /// <summary>
            ///     represents when the client has been detected as joining
            ///     by the log file, but has not be authenticated by RCon
            /// </summary>
            Connecting,

            /// <summary>
            ///     represents when the client has been authenticated by RCon
            ///     and validated by the database
            /// </summary>
            Connected,

            /// <summary>
            ///     represents when the client is leaving (either through RCon or log file)
            /// </summary>
            Disconnecting
        }

        public enum TeamType
        {
            Unknown,
            Spectator,
            Allies,
            Axis
        }

        [NotMapped] private readonly SemaphoreSlim _processingEvent;

        public EFClient()
        {
            ConnectionTime = DateTime.UtcNow;
            ClientNumber = -1;
            SetAdditionalProperty("_reportCount", 0);
            ReceivedPenalties = new List<EFPenalty>();
            _processingEvent = new SemaphoreSlim(1, 1);
        }

        [NotMapped]
        public virtual string Name
        {
            get => CurrentAlias?.Name ?? "--";
            set
            {
                if (CurrentAlias != null)
                {
                    CurrentAlias.Name = value;
                }
            }
        }

        [NotMapped] public string CleanedName => Name?.StripColors();

        [NotMapped]
        public virtual int? IPAddress
        {
            get => CurrentAlias?.IPAddress;
            set => CurrentAlias.IPAddress = value;
        }

        [NotMapped] public string IPAddressString => IPAddress.ConvertIPtoString();

        [NotMapped] public bool IsIngame => ClientNumber >= 0;

        [NotMapped] public virtual IDictionary<int, long> LinkedAccounts { get; set; }

        [NotMapped] public int ClientNumber { get; set; }

        [NotMapped] public int Ping { get; set; }

        [NotMapped] public int Warnings { get; set; }

        [NotMapped] public DateTime ConnectionTime { get; set; }

        [NotMapped] public int ConnectionLength => (int)(DateTime.UtcNow - ConnectionTime).TotalSeconds;

        [NotMapped] public Server CurrentServer { get; set; }

        [NotMapped] public int Score { get; set; }

        [NotMapped]
        public bool IsBot => NetworkId == Name.GenerateGuidFromString() ||
                             IPAddressString == System.Net.IPAddress.Broadcast.ToString() ||
                             IPAddressString == "unknown";

        [NotMapped] public bool IsZombieClient => IsBot && Name == "Zombie";

        [NotMapped] public string XuidString => (NetworkId + 0x110000100000000).ToString("x");

        [NotMapped] public string GuidString => NetworkId.ToString("x");

        [NotMapped] public ClientState State { get; set; }
        
        [NotMapped] public TeamType Team { get; set; }
        [NotMapped] public string TeamName { get; set; }

        [NotMapped]
        // this is kinda dirty, but I need localizable level names
        public ClientPermission ClientPermission => new ClientPermission
        {
            Level = Level,
            Name = Level.ToLocalizedLevelName()
        };

        [NotMapped]
        public string Tag
        {
            get => GetAdditionalProperty<string>(EFMeta.ClientTagV2);
            set => SetAdditionalProperty(EFMeta.ClientTagV2, value);
        }

        [NotMapped]
        public int TemporalClientNumber
        {
            get
            {
                var temporalClientId = GetAdditionalProperty<string>("ConnectionClientId");
                var parsedClientId = string.IsNullOrEmpty(temporalClientId) ? (int?)null : int.Parse(temporalClientId);
                return parsedClientId ?? ClientNumber;
            }
        }

        ~EFClient()
        {
            _processingEvent?.Dispose();
        }

        public override string ToString()
        {
            return
                $"[Name={CurrentAlias?.Name ?? "--"}, NetworkId={NetworkId.ToString("X")}, IP={(string.IsNullOrEmpty(IPAddressString) ? "--" : IPAddressString)}, ClientSlot={ClientNumber}]";
        }

        /// <summary>
        ///     send a message directly to the connected client
        /// </summary>
        /// <param name="message">message content to send to client</param>
        public GameEvent Tell(string message)
        {
            var e = new GameEvent
            {
                Message = message,
                Target = this,
                Owner = CurrentServer,
                Type = GameEvent.EventType.Tell,
                Data = message,
                CorrelationId = CurrentServer.Manager.ProcessingEvents.Values
                    .FirstOrDefault(ev =>
                        ev.Type == GameEvent.EventType.Command && (ev.Origin?.ClientId == ClientId ||
                                                                   ev.ImpersonationOrigin?.ClientId == ClientId))
                    ?.CorrelationId ?? Guid.NewGuid()
            };

            e.Output.Add(message.FormatMessageForEngine(CurrentServer?.RconParser.Configuration)
                .StripColors());

            CurrentServer?.Manager.AddEvent(e);
            return e;
        }

        [Obsolete("Use TellAsync")]
        public void Tell(IEnumerable<string> messages)
        {
            foreach (var message in messages)
            {
#pragma warning disable 4014
                Tell(message).WaitAsync();
#pragma warning restore 4014
            }
        }

        public async Task TellAsync(IEnumerable<string> messages, CancellationToken token = default)
        {
            foreach (var message in messages)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                
                await Tell(message).WaitAsync(Utilities.DefaultCommandTimeout, token);
            }
        }

        /// <summary>
        ///     warn a client with given reason
        /// </summary>
        /// <param name="warnReason">reason for warn</param>
        /// <param name="sender">client performing the warn</param>
        public GameEvent Warn(string warnReason, EFClient sender)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.Warn,
                Message = warnReason,
                Data = warnReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else
            {
                Warnings++;
            }

            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     clear all warnings for a client
        /// </summary>
        /// <param name="sender">client performing the warn clear</param>
        /// <returns></returns>
        public GameEvent WarnClear(EFClient sender)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.WarnClear,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (sender.Level <= Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
                return e;
            }

            Warnings = 0;

            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     report a client for a given reason
        /// </summary>
        /// <param name="reportReason">reason for the report</param>
        /// <param name="sender">client performing the report</param>
        /// <returns></returns>
        public GameEvent Report(string reportReason, EFClient sender)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.Report,
                Message = reportReason,
                Data = reportReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            var reportCount = sender.GetAdditionalProperty<int>("_reportCount");

            if (Equals(sender))
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            else if (reportCount > 2)
            {
                e.FailReason = GameEvent.EventFailReason.Throttle;
            }

            else if (CurrentServer.Reports.Count(report => report.Origin.NetworkId == sender.NetworkId &&
                                                           report.Target.NetworkId == NetworkId) > 0)
            {
                e.FailReason = GameEvent.EventFailReason.Exception;
            }

            sender.SetAdditionalProperty("_reportCount", reportCount + 1);
            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     flag a client for a given reason
        /// </summary>
        /// <param name="flagReason">reason for flagging</param>
        /// <param name="sender">client performing the flag</param>
        /// <param name="flagLength">how long the flag should last</param>
        /// <returns>game event for the flag</returns>
        public GameEvent Flag(string flagReason, EFClient sender, TimeSpan? flagLength = null)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.Flag,
                Origin = sender,
                Data = flagReason,
                Message = flagReason,
                Extra = flagLength,
                Target = this,
                Owner = sender.CurrentServer
            };

            if (Level >= sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else if (Level == Permission.Flagged || Level == Permission.Banned)
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     unflag a client for a given reason
        /// </summary>
        /// <param name="unflagReason">reason to unflag a player for</param>
        /// <param name="sender">client performing the unflag</param>
        /// <returns>game event for the un flug</returns>
        public GameEvent Unflag(string unflagReason, EFClient sender)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.Unflag,
                Origin = sender,
                Target = this,
                Data = unflagReason,
                Message = unflagReason,
                Owner = sender.CurrentServer
            };

            if (sender.Level <= Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else if (Level != Permission.Flagged || Level == Permission.Banned)
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     kick a client for the given reason
        /// </summary>
        /// <param name="kickReason">reason to kick for</param>
        /// <param name="sender">client performing the kick</param>
        public GameEvent Kick(string kickReason, EFClient sender)
        {
            return Kick(kickReason, sender, null);
        }

        /// <summary>
        ///     kick a client for the given reason
        /// </summary>
        /// <param name="kickReason">reason to kick for</param>
        /// <param name="sender">client performing the kick</param>
        /// <param name="originalPenalty">original client penalty</param>
        public GameEvent Kick(string kickReason, EFClient sender, EFPenalty originalPenalty)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.Kick,
                Message = kickReason,
                Target = this,
                Origin = sender,
                Data = kickReason,
                Extra = originalPenalty,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (sender.Level <= Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            State = ClientState.Disconnecting;
            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     temporarily ban a client for the given time span
        /// </summary>
        /// <param name="tempbanReason">reason for the temp ban</param>
        /// <param name="banLength">how long the temp ban lasts</param>
        /// <param name="sender">client performing the tempban</param>
        public GameEvent TempBan(string tempbanReason, TimeSpan banLength, EFClient sender)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.TempBan,
                Message = tempbanReason,
                Data = tempbanReason,
                Origin = sender,
                Target = this,
                Extra = banLength,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (sender.Level <= Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            State = ClientState.Disconnecting;
            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     permanently ban a client
        /// </summary>
        /// <param name="banReason">reason for the ban</param>
        /// <param name="sender">client performing the ban</param>
        /// <param name="isEvade">obsolete</param>
        public GameEvent Ban(string banReason, EFClient sender, bool isEvade)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.Ban,
                Message = banReason,
                Data = banReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer,
                Extra = isEvade
            };

            // enforce level restrictions
            if (sender.Level <= Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            if (Level == Permission.Banned)
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            State = ClientState.Disconnecting;
            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     unban a client
        /// </summary>
        /// <param name="unbanReason">reason for the unban</param>
        /// <param name="sender">client performing the unban</param>
        /// <returns></returns>
        public GameEvent Unban(string unbanReason, EFClient sender)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.Unban,
                Message = unbanReason,
                Data = unbanReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            // enforce level restrictions
            if (Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     sets the level of the client
        /// </summary>
        /// <param name="newPermission">new permission to set client to</param>
        /// <param name="sender">user performing the set level</param>
        /// <returns></returns>
        public GameEvent SetLevel(Permission newPermission, EFClient sender)
        {
            var e = new GameEvent
            {
                Type = GameEvent.EventType.ChangePermission,
                Extra = newPermission,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            if (Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else if (Level == newPermission)
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            else
            {
                Level = newPermission;
            }

            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        ///     Handles any client related logic on connection
        /// </summary>
        public bool IsAbleToConnectSimple()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            using (LogContext.PushProperty("Server", CurrentServer?.ToString()))
            {
                var nameToCheck = CurrentServer.IsCodGame() ? CleanedName : Name;
                if (string.IsNullOrWhiteSpace(Name) || nameToCheck.Replace(" ", "").Length <
                    (CurrentServer?.Manager?.GetApplicationSettings()?.Configuration()?.MinimumNameLength ?? 3))
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {Client} because their name is too short",
                        ToString());
                    Kick(loc["SERVER_KICK_MINNAME"], Utilities.IW4MAdminClient(CurrentServer));
                    return false;
                }

                if (CurrentServer.Manager.GetApplicationSettings().Configuration()
                        .DisallowedClientNames
                        ?.Any(_name => Regex.IsMatch(Name, _name)) ?? false)
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {Client} because their name is not allowed",
                        ToString());
                    Kick(loc["SERVER_KICK_GENERICNAME"], Utilities.IW4MAdminClient(CurrentServer));
                    return false;
                }

                if (Name.Where(c => char.IsControl(c)).Count() > 0)
                {
                    Utilities.DefaultLogger.LogInformation(
                        "Kicking {Client} because their name contains control characters", ToString());
                    Kick(loc["SERVER_KICK_CONTROLCHARS"], Utilities.IW4MAdminClient(CurrentServer));
                    return false;
                }

                // reserved slots stuff
                // todo: bots don't seem to honor party_maxplayers/sv_maxclients
                if (CurrentServer.MaxClients - CurrentServer.GetClientsAsList()
                        .Count(_client => !_client.IsPrivileged() && !_client.IsBot) <
                    CurrentServer.ServerConfig.ReservedSlotNumber &&
                    !this.IsPrivileged() &&
                    CurrentServer.GetClientsAsList().Count <= CurrentServer.MaxClients &&
                    CurrentServer.MaxClients != 0)
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {Client} their spot is reserved", ToString());
                    Kick(loc["SERVER_KICK_SLOT_IS_RESERVED"], Utilities.IW4MAdminClient(CurrentServer));
                    return false;
                }
            }

            return true;
        }

        public async Task OnDisconnect()
        {
            using (LogContext.PushProperty("Server", CurrentServer?.ToString()))
            {
                TotalConnectionTime += ConnectionLength;
                LastConnection = DateTime.UtcNow;

                Utilities.DefaultLogger.LogInformation("Client {client} is leaving the game", ToString());

                try
                {
                    await CurrentServer.Manager.GetClientService().Update(this);
                }

                catch (Exception e)
                {
                    Utilities.DefaultLogger.LogError(e, "Could not update disconnected client {client}",
                        ToString());
                }

                finally
                {
                    State = ClientState.Unknown;
                }
            }
        }

        public async Task OnJoin(int? ipAddress, bool enableImplicitLinking)
        {
            using (LogContext.PushProperty("Server", CurrentServer?.ToString()))
            {
                Utilities.DefaultLogger.LogInformation("Client {client} is joining the game from {source}", ToString(),
                    ipAddress.HasValue ? "Status" : "Log");

                GameName = (Reference.Game)CurrentServer.GameName;

                if (ipAddress != null)
                {
                    IPAddress = ipAddress;
                    Utilities.DefaultLogger.LogInformation("Received ip from client {client}", ToString());
                    await CurrentServer.Manager.GetClientService().UpdateAlias(this);
                    await CurrentServer.Manager.GetClientService().Update(this);

                    var canConnect = await CanConnect(ipAddress, enableImplicitLinking);

                    if (!canConnect)
                    {
                        Utilities.DefaultLogger.LogInformation("Client {client} is not allowed to join the server",
                            ToString());
                    }

                    else
                    {
                        Utilities.DefaultLogger.LogDebug("Creating join event for {client}", ToString());
                        var e = new GameEvent
                        {
                            Type = GameEvent.EventType.Join,
                            Origin = this,
                            Target = this,
                            Owner = CurrentServer
                        };

                        CurrentServer.Manager.AddEvent(e);
                    }
                }

                else
                {
                    Utilities.DefaultLogger.LogInformation("Waiting to receive ip from client {client}", ToString());
                }

                Utilities.DefaultLogger.LogDebug("OnJoin finished for {client}", ToString());
            }
        }

        public async Task<bool> CanConnect(int? ipAddress, bool enableImplicitLinking)
        {
            using (LogContext.PushProperty("Server", CurrentServer?.ToString()))
            {
                var loc = Utilities.CurrentLocalization.LocalizationIndex;
                var autoKickClient = Utilities.IW4MAdminClient(CurrentServer);
                var isAbleToConnectSimple = IsAbleToConnectSimple();

                if (!isAbleToConnectSimple)
                {
                    return false;
                }

                // we want to get any penalties that are tied to their IP or AliasLink (but not necessarily their GUID)
                var activePenalties = await CurrentServer.Manager.GetPenaltyService()
                    .GetActivePenaltiesAsync(AliasLinkId, CurrentAliasId, NetworkId, GameName, ipAddress);
                var banPenalty = activePenalties.FirstOrDefault(_penalty => _penalty.Type == EFPenalty.PenaltyType.Ban);
                var tempbanPenalty =
                    activePenalties.FirstOrDefault(_penalty => _penalty.Type == EFPenalty.PenaltyType.TempBan);
                var flagPenalty =
                    activePenalties.FirstOrDefault(_penalty => _penalty.Type == EFPenalty.PenaltyType.Flag);

                // we want to kick them if any account is banned
                if (banPenalty != null)
                {
                    if (Level != Permission.Banned)
                    {
                        Utilities.DefaultLogger.LogInformation(
                            "Client {Client} has a ban penalty, but they're using a new GUID, we we're updating their level and kicking them",
                            ToString());
                        
                        await SetLevel(Permission.Banned, autoKickClient).WaitAsync(Utilities.DefaultCommandTimeout,
                            CurrentServer.Manager.CancellationToken);
                    }

                    Utilities.DefaultLogger.LogInformation("Kicking {Client} because they are banned", ToString());
                    Kick(loc["WEBFRONT_PENALTY_LIST_BANNED_REASON"], autoKickClient, banPenalty);
                    return false;
                }

                // we want to kick them if any account is tempbanned
                if (tempbanPenalty != null)
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {client} because their GUID is temporarily banned",
                        ToString());
                    Kick(loc["WEBFRONT_PENALTY_LIST_TEMPBANNED_REASON"], autoKickClient, tempbanPenalty);
                    return false;
                }

                // if we found a flag, we need to make sure all the accounts are flagged
                if (flagPenalty != null && Level != Permission.Flagged)
                {
                    Utilities.DefaultLogger.LogInformation(
                        "Flagged client {client} joining with new GUID, so we are changing their level to flagged",
                        ToString());
                    await SetLevel(Permission.Flagged, autoKickClient).WaitAsync(Utilities.DefaultCommandTimeout,
                        CurrentServer.Manager.CancellationToken);
                }

                // remove their auto flag
                if (Level == Permission.Flagged &&
                    !activePenalties.Any(_penalty => _penalty.Type == EFPenalty.PenaltyType.Flag))
                {
                    // remove their auto flag status after a week
                    Utilities.DefaultLogger.LogInformation("Unflagging {client} because the auto flag time has expired",
                        ToString());
                    Unflag(Utilities.CurrentLocalization.LocalizationIndex["SERVER_AUTOFLAG_UNFLAG"], autoKickClient);
                }

                if (Level != Permission.Banned)
                {
                    return true;
                }

                // we want to see if they've recently used a banned IP
                var recentIPPenalties= await CurrentServer.Manager.GetPenaltyService().ActivePenaltiesByRecentIdentifiers(AliasLinkId);
                    
                var recentBanPenalty =
                    recentIPPenalties.FirstOrDefault(penalty => penalty.Type == EFPenalty.PenaltyType.Ban);

                if (recentBanPenalty is null || !IPAddress.HasValue)
                {
                    Utilities.DefaultLogger.LogInformation(
                        "Setting {Client} level to user because they are banned but no direct penalties or recent penalty identifiers exist for them",
                        ToString());
                    await SetLevel(Permission.User, autoKickClient).WaitAsync(Utilities.DefaultCommandTimeout,
                        CurrentServer.Manager.CancellationToken);   
                    return true;
                }

                Utilities.DefaultLogger.LogInformation("Updating penalty for {Client} because they recently used a banned IP", this);
                await CurrentServer.Manager.GetPenaltyService()
                    .CreatePenaltyIdentifier(recentBanPenalty.PenaltyId, NetworkId, IPAddress.Value);
                        
                Utilities.DefaultLogger.LogInformation("Kicking {Client} because they are banned", ToString());
                Kick(loc["WEBFRONT_PENALTY_LIST_BANNED_REASON"], autoKickClient, recentBanPenalty);
            }

            return true;
        }

        public void UpdateTeam(string newTeam)
        {
            if (string.IsNullOrEmpty(newTeam))
            {
                return;
            }

            Team = Enum.TryParse(newTeam, true, out TeamType team) ? team : TeamType.Unknown;
            TeamName = newTeam;
        }

        public async Task Lock()
        {
            var result = await _processingEvent.WaitAsync(Utilities.DefaultCommandTimeout);

#if DEBUG
            if (!result)
            {
                throw new InvalidOperationException();
            }
#endif
        }

        public void Unlock()
        {
            if (_processingEvent.CurrentCount == 0)
            {
                _processingEvent.Release(1);
            }
        }

        public override bool Equals(object obj)
        {
            return obj.GetType() == typeof(EFClient) && ((EFClient)obj).NetworkId == NetworkId;
        }

        public override int GetHashCode()
        {
            return IsBot ? ClientNumber : (int)NetworkId;
        }
    }
}
