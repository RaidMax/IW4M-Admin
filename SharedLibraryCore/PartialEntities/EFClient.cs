using Newtonsoft.Json.Converters;
using SharedLibraryCore.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace SharedLibraryCore.Database.Models
{
    public partial class EFClient
    {
        public enum ClientState
        {
            /// <summary>
            /// default client state
            /// </summary>
            Unknown,

            /// <summary>
            /// represents when the client has been detected as joining
            /// by the log file, but has not be authenticated by RCon
            /// </summary>
            Connecting,

            /// <summary>
            /// represents when the client has been authenticated by RCon
            /// and validated by the database
            /// </summary>
            Connected,

            /// <summary>
            /// represents when the client is leaving (either through RCon or log file)
            /// </summary>
            Disconnecting
        }

        public enum Permission
        {
            /// <summary>
            /// client has been banned
            /// </summary>
            Banned = -1,
            /// <summary>
            /// default client state upon first connect
            /// </summary>
            User = 0,
            /// <summary>
            /// client has been flagged
            /// </summary>
            Flagged = 1,
            /// <summary>
            /// client is trusted
            /// </summary>
            Trusted = 2,
            /// <summary>
            /// client is a moderator
            /// </summary>
            Moderator = 3,
            /// <summary>
            /// client is an administrator
            /// </summary>
            Administrator = 4,
            /// <summary>
            /// client is a senior administrator
            /// </summary>
            SeniorAdmin = 5,
            /// <summary>
            /// client is a owner
            /// </summary>
            Owner = 6,
            /// <summary>
            /// not used
            /// </summary>
            Creator = 7,
            /// <summary>
            /// reserved for default account
            /// </summary>
            Console = 8
        }

        public EFClient()
        {
            ConnectionTime = DateTime.UtcNow;
            ClientNumber = -1;
            SetAdditionalProperty("_reportCount", 0);
            ReceivedPenalties = new List<EFPenalty>();
            _processingEvent = new SemaphoreSlim(1, 1);
            
        }

        ~EFClient()
        {
            _processingEvent.Dispose();
        }

        public override string ToString()
        {
            return $"[Name={CurrentAlias?.Name ?? "--"}, NetworkId={NetworkId.ToString("X")}, IP={(string.IsNullOrEmpty(IPAddressString) ? "--" : IPAddressString)}, ClientSlot={ClientNumber}]";
        }

        [NotMapped]
        public virtual string Name
        {
            get { return CurrentAlias?.Name ?? "--"; }
            set { if (CurrentAlias != null) CurrentAlias.Name = value; }
        }

        [NotMapped]
        public string CleanedName => Name?.StripColors();

        [NotMapped]
        public virtual int? IPAddress
        {
            get { return CurrentAlias.IPAddress; }
            set { CurrentAlias.IPAddress = value; }
        }

        [NotMapped]
        public string IPAddressString => IPAddress.ConvertIPtoString();

        [NotMapped]
        public bool IsIngame => ClientNumber >= 0;

        [NotMapped]
        public virtual IDictionary<int, long> LinkedAccounts { get; set; }

        /// <summary>
        /// send a message directly to the connected client
        /// </summary>
        /// <param name="message">message content to send to client</param>
        public GameEvent Tell(String message)
        {
            var e = new GameEvent()
            {
                Message = message,
                Target = this,
                Owner = CurrentServer,
                Type = GameEvent.EventType.Tell,
                Data = message
            };

            CurrentServer?.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        /// warn a client with given reason
        /// </summary>
        /// <param name="warnReason">reason for warn</param>
        /// <param name="sender">client performing the warn</param>
        public GameEvent Warn(String warnReason, EFClient sender)
        {
            var e = new GameEvent()
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
        /// clear all warnings for a client
        /// </summary>
        /// <param name="sender">client performing the warn clear</param>
        /// <returns></returns>
        public GameEvent WarnClear(EFClient sender)
        {
            var e = new GameEvent()
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
        /// report a client for a given reason
        /// </summary>
        /// <param name="reportReason">reason for the report</param>
        /// <param name="sender">client performing the report</param>
        /// <returns></returns>
        public GameEvent Report(string reportReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Report,
                Message = reportReason,
                Data = reportReason,
                Origin = sender,
                Target = this,
                Owner = sender.CurrentServer
            };

            int reportCount = sender.GetAdditionalProperty<int>("_reportCount");

            if (Equals(sender))
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            else if (reportCount > 2)
            {
                e.FailReason = GameEvent.EventFailReason.Throttle;
            }

            else if (CurrentServer.Reports.Count(report => (report.Origin.NetworkId == sender.NetworkId &&
                report.Target.NetworkId == NetworkId)) > 0)
            {
                e.FailReason = GameEvent.EventFailReason.Exception;
            }

            sender.SetAdditionalProperty("_reportCount", reportCount + 1);
            sender.CurrentServer.Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        /// flag a client for a given reason
        /// </summary>
        /// <param name="flagReason">reason for flagging</param>
        /// <param name="sender">client performing the flag</param>
        /// <returns>game event for the flag</returns>
        public GameEvent Flag(string flagReason, EFClient sender, TimeSpan? flagLength = null)
        {
            var e = new GameEvent()
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
        /// unflag a client for a given reason
        /// </summary>
        /// <param name="unflagReason">reason to unflag a player for</param>
        /// <param name="sender">client performing the unflag</param>
        /// <returns>game event for the un flug</returns>
        public GameEvent Unflag(string unflagReason, EFClient sender)
        {
            var e = new GameEvent()
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
        /// kick a client for the given reason
        /// </summary>
        /// <param name="kickReason">reason to kick for</param>
        /// <param name="sender">client performing the kick</param>
        public GameEvent Kick(string kickReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Kick,
                Message = kickReason,
                Target = this,
                Origin = sender,
                Data = kickReason,
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
        /// temporarily ban a client for the given time span
        /// </summary>
        /// <param name="tempbanReason">reason for the temp ban</param>
        /// <param name="banLength">how long the temp ban lasts</param>
        /// <param name="sender">client performing the tempban</param>
        public GameEvent TempBan(String tempbanReason, TimeSpan banLength, EFClient sender)
        {
            var e = new GameEvent()
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
        /// permanently ban a client
        /// </summary>
        /// <param name="banReason">reason for the ban</param>
        /// <param name="sender">client performing the ban</param>
        public GameEvent Ban(String banReason, EFClient sender, bool isEvade)
        {
            var e = new GameEvent()
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
        /// unban a client
        /// </summary>
        /// <param name="unbanReason">reason for the unban</param>
        /// <param name="sender">client performing the unban</param>
        /// <returns></returns>
        public GameEvent Unban(string unbanReason, EFClient sender)
        {
            var e = new GameEvent()
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
        /// sets the level of the client
        /// </summary>
        /// <param name="newPermission">new permission to set client to</param>
        /// <param name="sender">user performing the set level</param>
        /// <returns></returns>
        public GameEvent SetLevel(Permission newPermission, EFClient sender)
        {
            var e = new GameEvent()
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
        /// Handles any client related logic on connection
        /// </summary>
        public bool IsAbleToConnectSimple()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            using (LogContext.PushProperty("Server", CurrentServer?.ToString()))
            {
                if (string.IsNullOrWhiteSpace(Name) || CleanedName.Replace(" ", "").Length < 3)
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {client} because their name is too short", ToString());
                    Kick(loc["SERVER_KICK_MINNAME"], Utilities.IW4MAdminClient(CurrentServer));
                    return false;
                }

                if (CurrentServer.Manager.GetApplicationSettings().Configuration()
                    .DisallowedClientNames
                    ?.Any(_name => Regex.IsMatch(Name, _name)) ?? false)
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {client} because their name is not allowed", ToString());
                    Kick(loc["SERVER_KICK_GENERICNAME"], Utilities.IW4MAdminClient(CurrentServer));
                    return false;
                }

                if (Name.Where(c => char.IsControl(c)).Count() > 0)
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {client} because their name contains control characters", ToString());
                    Kick(loc["SERVER_KICK_CONTROLCHARS"], Utilities.IW4MAdminClient(CurrentServer));
                    return false;
                }

                // reserved slots stuff
                // todo: bots don't seem to honor party_maxplayers/sv_maxclients
                if (CurrentServer.MaxClients - (CurrentServer.GetClientsAsList().Count(_client => !_client.IsPrivileged() && !_client.IsBot)) < CurrentServer.ServerConfig.ReservedSlotNumber &&
                !this.IsPrivileged() &&
                CurrentServer.GetClientsAsList().Count <= CurrentServer.MaxClients &&
                CurrentServer.MaxClients != 0)
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {client} their spot is reserved", ToString());
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

        public async Task OnJoin(int? ipAddress)
        {
            using (LogContext.PushProperty("Server", CurrentServer?.ToString()))
            {
                Utilities.DefaultLogger.LogInformation("Client {client} is joining the game from {source}", ToString(), ipAddress.HasValue ? "Status" : "Log");

                if (ipAddress != null)
                {
                    IPAddress = ipAddress;
                    Utilities.DefaultLogger.LogInformation("Received ip from client {client}", ToString());
                    await CurrentServer.Manager.GetClientService().UpdateAlias(this);
                    await CurrentServer.Manager.GetClientService().Update(this);

                    bool canConnect = await CanConnect(ipAddress);

                    if (!canConnect)
                    {
                        Utilities.DefaultLogger.LogInformation("Client {client} is not allowed to join the server",
                            ToString());
                    }

                    else
                    {
                        Utilities.DefaultLogger.LogDebug("Creating join event for {client}", ToString());
                        var e = new GameEvent()
                        {
                            Type = GameEvent.EventType.Join,
                            Origin = this,
                            Target = this,
                            Owner = CurrentServer,
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

        public async Task<bool> CanConnect(int? ipAddress)
        {
            using (LogContext.PushProperty("Server", CurrentServer?.ToString()))
            {
                var loc = Utilities.CurrentLocalization.LocalizationIndex;
                var autoKickClient = Utilities.IW4MAdminClient(CurrentServer);

                bool isAbleToConnectSimple = IsAbleToConnectSimple();

                if (!isAbleToConnectSimple)
                {
                    return false;
                }

                // we want to get any penalties that are tied to their IP or AliasLink (but not necessarily their GUID)
                var activePenalties = await CurrentServer.Manager.GetPenaltyService()
                    .GetActivePenaltiesAsync(AliasLinkId, ipAddress);
                var banPenalty = activePenalties.FirstOrDefault(_penalty => _penalty.Type == EFPenalty.PenaltyType.Ban);
                var tempbanPenalty =
                    activePenalties.FirstOrDefault(_penalty => _penalty.Type == EFPenalty.PenaltyType.TempBan);
                var flagPenalty =
                    activePenalties.FirstOrDefault(_penalty => _penalty.Type == EFPenalty.PenaltyType.Flag);

                // we want to kick them if any account is banned
                if (banPenalty != null)
                {
                    if (Level == Permission.Banned)
                    {
                        Utilities.DefaultLogger.LogInformation("Kicking {client} because they are banned", ToString());
                        Kick(loc["SERVER_BAN_PREV"].FormatExt(banPenalty?.Offense), autoKickClient);
                        return false;
                    }

                    else
                    {
                        Utilities.DefaultLogger.LogInformation(
                            "Client {client} is banned, but using a new GUID, we we're updating their level and kicking them",
                            ToString());
                        await SetLevel(Permission.Banned, autoKickClient).WaitAsync(Utilities.DefaultCommandTimeout,
                            CurrentServer.Manager.CancellationToken);
                        Kick(loc["SERVER_BAN_PREV"].FormatExt(banPenalty?.Offense), autoKickClient);
                        return false;
                    }
                }

                // we want to kick them if any account is tempbanned
                if (tempbanPenalty != null)
                {
                    Utilities.DefaultLogger.LogInformation("Kicking {client} because their GUID is temporarily banned",
                        ToString());
                    Kick(
                        $"{loc["SERVER_TB_REMAIN"]} ({(tempbanPenalty.Expires.Value - DateTime.UtcNow).HumanizeForCurrentCulture()} {loc["WEBFRONT_PENALTY_TEMPLATE_REMAINING"]})",
                        autoKickClient);
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
            }

            return true;
        }

        [NotMapped]
        public int ClientNumber { get; set; }
        [NotMapped]
        public int Ping { get; set; }
        [NotMapped]
        public int Warnings { get; set; }
        [NotMapped]
        public DateTime ConnectionTime { get; set; }
        [NotMapped]
        public int ConnectionLength => (int)(DateTime.UtcNow - ConnectionTime).TotalSeconds;
        [NotMapped]
        public Server CurrentServer { get; set; }
        [NotMapped]
        public int Score { get; set; }
        [NotMapped]
        public bool IsBot => NetworkId == Name.GenerateGuidFromString();
        [NotMapped]
        public bool IsZombieClient => IsBot && Name == "Zombie";
        [NotMapped]
        public string XuidString => (NetworkId + 0x110000100000000).ToString("x");
        [NotMapped]
        public string GuidString => NetworkId.ToString("x");

        [NotMapped]
        public ClientState State { get; set; }

        [NotMapped]
        // this is kinda dirty, but I need localizable level names
        public ClientPermission ClientPermission => new ClientPermission()
        {
            Level = Level,
            Name = Utilities.CurrentLocalization
                .LocalizationIndex[$"GLOBAL_PERMISSION_{Level.ToString().ToUpper()}"]
        };

        [NotMapped]
        private readonly SemaphoreSlim _processingEvent;

        public async Task Lock()
        {
            bool result = await _processingEvent.WaitAsync(Utilities.DefaultCommandTimeout);

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
            return obj.GetType() == typeof(EFClient) && ((EFClient)obj).NetworkId == this.NetworkId;
        }

        public override int GetHashCode()
        {
            return IsBot ? ClientNumber : (int)NetworkId;
        }
    }
}
