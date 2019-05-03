using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharedLibraryCore.Database.Models
{
    public partial class EFClient
    {
        public enum ClientState
        {
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
            Disconnecting,
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
            _additionalProperties = new Dictionary<string, object>
            {
                { "_reportCount", 0 }
            };
            ReceivedPenalties = new List<EFPenalty>();
        }

        public override string ToString()
        {
            return $"{CurrentAlias?.Name ?? "--"}::{NetworkId}";
        }

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

            CurrentServer?.Manager.GetEventHandler().AddEvent(e);
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

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
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

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
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

            if (Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else if (Equals(sender))
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
            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// flag a client for a given reason
        /// </summary>
        /// <param name="flagReason">reason for flagging</param>
        /// <param name="sender">client performing the flag</param>
        /// <returns>game event for the flag</returns>
        public GameEvent Flag(string flagReason, EFClient sender)
        {
            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Flag,
                Origin = sender,
                Data = flagReason,
                Message = flagReason,
                Target = this,
                Owner = sender.CurrentServer
            };

            if (Level >= sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            else if (Level == Permission.Flagged)
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
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

            else if (Level != Permission.Flagged)
            {
                e.FailReason = GameEvent.EventFailReason.Invalid;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// kick a client for the given reason
        /// </summary>
        /// <param name="kickReason">reason to kick for</param>
        /// <param name="sender">client performing the kick</param>
        public GameEvent Kick(String kickReason, EFClient sender)
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
            if (Level > sender.Level)
            {
                e.FailReason = GameEvent.EventFailReason.Permission;
            }

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
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

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
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

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
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

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
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

            sender.CurrentServer.Manager.GetEventHandler().AddEvent(e);
            return e;
        }

        /// <summary>
        /// Handles any client related logic on connection
        /// </summary>
        public void OnConnect()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            LastConnection = DateTime.UtcNow;
            Connections += 1;

            if (Name.Length < 3)
            {
                CurrentServer.Logger.WriteDebug($"Kicking {this} because their name is too short");
                Kick(loc["SERVER_KICK_MINNAME"], Utilities.IW4MAdminClient(CurrentServer));
                return;
            }

            if (CurrentServer.Manager.GetApplicationSettings().Configuration()
                .DisallowedClientNames
                ?.Any(_name => Regex.IsMatch(Name, _name)) ?? false)
            {
                CurrentServer.Logger.WriteDebug($"Kicking {this} because their name is generic");
                Kick(loc["SERVER_KICK_GENERICNAME"], Utilities.IW4MAdminClient(CurrentServer));
                return;
            }

            if (Name.Where(c => char.IsControl(c)).Count() > 0)
            {
                CurrentServer.Logger.WriteDebug($"Kicking {this} because their name contains control characters");
                Kick(loc["SERVER_KICK_CONTROLCHARS"], Utilities.IW4MAdminClient(CurrentServer));
                return;
            }

            // reserved slots stuff
            // todo: bots don't seem to honor party_maxplayers/sv_maxclients
            if (CurrentServer.MaxClients - (CurrentServer.GetClientsAsList().Count(_client => !_client.IsPrivileged() && !_client.IsBot)) < CurrentServer.ServerConfig.ReservedSlotNumber &&
               !this.IsPrivileged() &&
               CurrentServer.GetClientsAsList().Count <= CurrentServer.MaxClients &&
               CurrentServer.MaxClients != 0)
            {
                CurrentServer.Logger.WriteDebug($"Kicking {this} their spot is reserved");
                Kick(loc["SERVER_KICK_SLOT_IS_RESERVED"], Utilities.IW4MAdminClient(CurrentServer));
                return;
            }
        }

        public async Task OnDisconnect()
        {
            State = ClientState.Disconnecting;
            TotalConnectionTime += ConnectionLength;
            LastConnection = DateTime.UtcNow;

            try
            {
                await CurrentServer.Manager.GetClientService().Update(this);
            }

            catch (Exception e)
            {
                CurrentServer.Logger.WriteWarning($"Could not update disconnected player {this}");
                CurrentServer.Logger.WriteDebug(e.GetExceptionInfo());
            }
        }

        public async Task OnJoin(int? ipAddress)
        {
            CurrentServer.Logger.WriteDebug($"Start join for {this}::{ipAddress}::{Level.ToString()}");

            if (ipAddress != null)
            {
                IPAddress = ipAddress;
                await CurrentServer.Manager.GetClientService().UpdateAlias(this);
            }

            // we want to run any non GUID based logic here
            OnConnect();

            if (await CanConnect(ipAddress))
            {
                if (IPAddress != null)
                {
                    await CurrentServer.Manager.GetClientService().Update(this);

                    var e = new GameEvent()
                    {
                        Type = GameEvent.EventType.Join,
                        Origin = this,
                        Target = this,
                        Owner = CurrentServer
                    };

                    CurrentServer.Manager.GetEventHandler().AddEvent(e);
                }
            }

            else
            {
                CurrentServer.Logger.WriteDebug($"Client {this} is not allowed to join the server");
            }

            CurrentServer.Logger.WriteDebug($"OnJoin finished for {this}");
        }

        private async Task<bool> CanConnect(int? ipAddress)
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;
            var autoKickClient = Utilities.IW4MAdminClient(CurrentServer);

            #region CLIENT_GUID_BAN
            // kick them as their level is banned
            if (Level == Permission.Banned)
            {
                var profileBan = ReceivedPenalties.FirstOrDefault(_penalty => _penalty.Expires == null && _penalty.Active && _penalty.Type == Penalty.PenaltyType.Ban);

                if (profileBan == null)
                {
                    // this is from the old system before bans were applied to all accounts
                    profileBan = (await CurrentServer.Manager
                        .GetPenaltyService()
                        .GetActivePenaltiesAsync(AliasLinkId))
                        .FirstOrDefault(_penalty => _penalty.Type == Penalty.PenaltyType.Ban);

                    CurrentServer.Logger.WriteWarning($"Client {this} is GUID banned, but no previous penalty exists for their ban");

                    if (profileBan == null)
                    {
                        profileBan = new EFPenalty() { Offense = loc["SERVER_BAN_UNKNOWN"] };
                        CurrentServer.Logger.WriteWarning($"Client {this} is GUID banned, but we could not find the penalty on any linked accounts");
                    }

                    // hack: re apply the automated offense to the reban
                    if (profileBan.AutomatedOffense != null)
                    {
                        autoKickClient.AdministeredPenalties?.Add(new EFPenalty()
                        {
                            AutomatedOffense = profileBan.AutomatedOffense
                        });
                    }

                    // this is a reban of the new GUID and IP
                    Ban($"{profileBan.Offense}", autoKickClient, false);
                    return false;
                }

                CurrentServer.Logger.WriteDebug($"Kicking {this} because they are banned");
                Kick(loc["SERVER_BAN_PREV"].FormatExt(profileBan?.Offense), autoKickClient);
                return false;
            }
            #endregion

            #region CLIENT_GUID_TEMPBAN
            else
            {
                var profileTempBan = ReceivedPenalties.FirstOrDefault(_penalty => _penalty.Type == Penalty.PenaltyType.TempBan &&
                    _penalty.Active &&
                    _penalty.Expires > DateTime.UtcNow);

                // they have an active tempban tied to their GUID
                if (profileTempBan != null)
                {
                    CurrentServer.Logger.WriteDebug($"Kicking {this} because their GUID is temporarily banned");
                    Kick($"{loc["SERVER_TB_REMAIN"]} ({(profileTempBan.Expires.Value - DateTime.UtcNow).TimeSpanText()} {loc["WEBFRONT_PENALTY_TEMPLATE_REMAINING"]})", autoKickClient);
                    return false;
                }
            }
            #endregion

            // we want to get any penalties that are tied to their IP or AliasLink (but not necessarily their GUID)
            var activePenalties = await CurrentServer.Manager.GetPenaltyService().GetActivePenaltiesAsync(AliasLinkId, ipAddress);

            #region CLIENT_LINKED_TEMPBAN
            var tempBan = activePenalties.FirstOrDefault(_penalty => _penalty.Type == Penalty.PenaltyType.TempBan);

            // they have an active tempban tied to their AliasLink
            if (tempBan != null)
            {
                CurrentServer.Logger.WriteDebug($"Tempbanning {this} because their AliasLink is temporarily banned, but they are not");
                TempBan(tempBan.Offense, DateTime.UtcNow - (tempBan.Expires ?? DateTime.UtcNow), autoKickClient);
                return false;
            }
            #endregion

            #region CLIENT_LINKED_BAN
            var currentBan = activePenalties.FirstOrDefault(p => p.Type == Penalty.PenaltyType.Ban);

            // they have a perm ban tied to their AliasLink/profile
            if (currentBan != null)
            {
                CurrentServer.Logger.WriteInfo($"Banned client {this} trying to evade...");

                // reban the "evading" guid
                if (Level != Permission.Banned)
                {
                    CurrentServer.Logger.WriteInfo($"Banned client {this} connected using a new GUID");

                    // hack: re apply the automated offense to the reban
                    if (currentBan.AutomatedOffense != null)
                    {
                        autoKickClient.AdministeredPenalties?.Add(new EFPenalty()
                        {
                            AutomatedOffense = currentBan.AutomatedOffense
                        });
                    }
                    // this is a reban of the new GUID and IP
                    Ban($"{currentBan.Offense}", autoKickClient, true);
                }

                else
                {
                    CurrentServer.Logger.WriteError($"Banned client {this} is banned but, no ban penalty was found (2)");
                }

                return false;
            }
            #endregion

            #region CLIENT_LINKED_FLAG
            if (Level != Permission.Flagged)
            {
                var currentFlag = activePenalties.FirstOrDefault(_penalty => _penalty.Type == Penalty.PenaltyType.Flag);

                if (currentFlag != null)
                {
                    CurrentServer.Logger.WriteDebug($"Flagging {this} because their AliasLink is flagged, but they are not");
                    Flag(currentFlag.Offense, autoKickClient);
                }
            }
            #endregion

            if (Level == Permission.Flagged)
            {
                var currentAutoFlag = activePenalties
                    .Where(p => p.Type == Penalty.PenaltyType.Flag && p.PunisherId == 1)
                    .OrderByDescending(p => p.When)
                    .FirstOrDefault();

                // remove their auto flag status after a week
                if (currentAutoFlag != null &&
                    (DateTime.UtcNow - currentAutoFlag.When).TotalDays > 7)
                {
                    CurrentServer.Logger.WriteInfo($"Unflagging {this} because the auto flag time has expired");
                    Unflag(Utilities.CurrentLocalization.LocalizationIndex["SERVER_AUTOFLAG_UNFLAG"], autoKickClient);
                }
            }

            return true;
        }

        [NotMapped]
        readonly Dictionary<string, object> _additionalProperties;

        public T GetAdditionalProperty<T>(string name)
        {
            return _additionalProperties.ContainsKey(name) ? (T)_additionalProperties[name] : default(T);
        }

        public void SetAdditionalProperty(string name, object value)
        {
            if (_additionalProperties.ContainsKey(name))
            {
                _additionalProperties[name] = value;
            }
            else
            {
                _additionalProperties.Add(name, value);
            }
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
        public bool IsBot { get; set; }

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

        public override bool Equals(object obj)
        {
            return ((EFClient)obj).NetworkId == this.NetworkId;
        }

        public override int GetHashCode()
        {
            return (int)NetworkId;
        }
    }
}
