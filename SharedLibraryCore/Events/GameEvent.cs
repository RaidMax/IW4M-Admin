﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore
{
    public class GameEvent
    {
        public enum EventFailReason
        {
            /// <summary>
            ///     event execution did not fail
            /// </summary>
            None,

            /// <summary>
            ///     an internal exception prevented the event
            ///     from executing
            /// </summary>
            Exception,

            /// <summary>
            ///     event origin didn't have the necessary privileges
            ///     to execute the command
            /// </summary>
            Permission,

            /// <summary>
            ///     executing the event would cause an invalid state
            /// </summary>
            Invalid,

            /// <summary>
            ///     client is doing too much of something
            /// </summary>
            Throttle,

            /// <summary>
            ///     the event timed out before completion
            /// </summary>
            Timeout
        }

        [Flags]
        public enum EventRequiredEntity
        {
            None = 1,
            Origin = 2,
            Target = 4
        }

        public enum EventSource
        {
            Unspecified,
            Log,
            Status,
            Internal
        }

        public enum EventType
        {
            /// <summary>
            ///     the event wasn't parsed properly
            /// </summary>
            Unknown,

            // events "generated" by the server
            /// <summary>
            ///     a server started being monitored
            /// </summary>
            Start,

            /// <summary>
            ///     a server stopped being monitored
            /// </summary>
            Stop,

            /// <summary>
            ///     a client was detecting as connecting via log
            /// </summary>
            Connect,

            /// <summary>
            ///     a client was detecting joining by RCon
            /// </summary>
            Join,

            /// <summary>
            ///     a client was detected leaving via log
            /// </summary>
            Quit,

            /// <summary>
            ///     a client was detected leaving by RCon
            /// </summary>
            Disconnect,

            /// <summary>
            ///     the current map ended
            /// </summary>
            MapEnd,

            /// <summary>
            ///     the current map changed
            /// </summary>
            MapChange,

            /// <summary>
            ///     a client was detected as starting to connect
            /// </summary>
            PreConnect,

            /// <summary>
            ///     a client was detecting as starting to disconnect
            /// </summary>
            PreDisconnect,

            /// <summary>
            ///     a client's information was updated
            /// </summary>
            Update,

            /// <summary>
            ///     connection was lost to a server (the server has not responded after a number of attempts)
            /// </summary>
            ConnectionLost,

            /// <summary>
            ///     connection was restored to a server (the server began responding again)
            /// </summary>
            ConnectionRestored,

            // events "generated" by clients  
            /// <summary>
            ///     a client sent a message
            /// </summary>
            Say = 100,

            /// <summary>
            ///     a client was warned
            /// </summary>
            Warn = 101,

            /// <summary>
            ///     all warnings for a client were cleared
            /// </summary>
            WarnClear = 102,

            /// <summary>
            ///     a client was reported
            /// </summary>
            Report = 103,

            /// <summary>
            ///     a client was flagged
            /// </summary>
            Flag = 104,

            /// <summary>
            ///     a client was unflagged
            /// </summary>
            Unflag = 105,

            /// <summary>
            ///     a client was kicked
            /// </summary>
            Kick = 106,

            /// <summary>
            ///     a client was tempbanned
            /// </summary>
            TempBan = 107,

            /// <summary>
            ///     a client was banned
            /// </summary>
            Ban = 108,

            /// <summary>
            ///     a client was unbanned
            /// </summary>
            Unban = 109,

            /// <summary>
            ///     a client entered a command
            /// </summary>
            Command = 110,

            /// <summary>
            ///     a client's permission was changed
            /// </summary>
            ChangePermission = 111,

            /// <summary>
            ///     client logged in to webfront
            /// </summary>
            Login = 112,

            /// <summary>
            ///     client logged out of webfront
            /// </summary>
            Logout = 113,

            // events "generated" by IW4MAdmin
            /// <summary>
            ///     a message is sent to all clients
            /// </summary>
            Broadcast = 200,

            /// <summary>
            ///     a message is sent to a specific client
            /// </summary>
            Tell = 201,

            // events "generated" by script/log
            /// <summary>
            ///     AC Damage Log
            /// </summary>
            ScriptDamage = 300,

            /// <summary>
            ///     AC Kill Log
            /// </summary>
            ScriptKill = 301,

            /// <summary>
            ///     damage info printed out by game script
            /// </summary>
            Damage = 302,

            /// <summary>
            ///     kill info printed out by game script
            /// </summary>
            Kill = 303,

            /// <summary>
            ///     team info printed out by game script
            /// </summary>
            JoinTeam = 304,

            /// <summary>
            ///     used for community generated plugin events
            /// </summary>
            Other
        }

        private static long NextEventId;
        private readonly ManualResetEvent _eventFinishedWaiter;
        public string Data; // Data is usually the message sent by player
        public string Message;
        public EFClient Origin;
        public Server Owner;
        public EFClient Target;

        public EventType Type;

        public GameEvent()
        {
            _eventFinishedWaiter = new ManualResetEvent(false);
            Time = DateTime.UtcNow;
            Id = GetNextEventId();
        }

        public EventSource Source { get; set; }

        /// <summary>
        ///     suptype of the event for more detailed classification
        /// </summary>
        public string Subtype { get; set; }

        public EventRequiredEntity RequiredEntity { get; set; }

        /// <summary>
        ///     Specifies the game time offset as printed in the log
        /// </summary>
        public long? GameTime { get; set; }

        public EFClient ImpersonationOrigin { get; set; }
        public bool IsRemote { get; set; }
        public object Extra { get; set; }
        public DateTime Time { get; set; }
        public long Id { get; }
        public EventFailReason FailReason { get; set; }
        public bool Failed => FailReason != EventFailReason.None;
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        public List<string> Output { get; set; } = new List<string>();

        /// <summary>
        ///     Indicates if the event should block until it is complete
        /// </summary>
        public bool IsBlocking { get; set; }

        private static long GetNextEventId()
        {
            return Interlocked.Increment(ref NextEventId);
        }

        ~GameEvent()
        {
            _eventFinishedWaiter.Set();
            _eventFinishedWaiter.Dispose();
        }

        public void Complete()
        {
            _eventFinishedWaiter.Set();
        }

        public async Task<GameEvent> WaitAsync()
        {
            return await WaitAsync(Utilities.DefaultCommandTimeout, new CancellationToken());
        }

        /// <summary>
        ///     asynchronously wait for GameEvent to be processed
        /// </summary>
        /// <returns>waitable task </returns>
        public async Task<GameEvent> WaitAsync(TimeSpan timeSpan, CancellationToken token)
        {
            var processed = false;
            Utilities.DefaultLogger.LogDebug("Begin wait for event {Id}", Id);
            try
            {
                processed = await Task.Run(() => _eventFinishedWaiter.WaitOne(timeSpan), token);
            }

            catch (TaskCanceledException)
            {
                processed = true;
            }

            if (!processed)
            {
                using (LogContext.PushProperty("Server", Owner?.ToString()))
                {
                    Utilities.DefaultLogger.LogError("Waiting for event to complete timed out {@eventData}",
                        new { Event = this, Message, Origin = Origin?.ToString(), Target = Target?.ToString() });
                }
            }

            // this lets us know if the the action timed out
            FailReason = FailReason == EventFailReason.None && !processed ? EventFailReason.Timeout : FailReason;
            return this;
        }
    }
}