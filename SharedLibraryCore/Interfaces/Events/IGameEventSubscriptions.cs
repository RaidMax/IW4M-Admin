using System;
using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.Events;
using SharedLibraryCore.Events.Game;

namespace SharedLibraryCore.Interfaces.Events;

public interface IGameEventSubscriptions
{
    /// <summary>
    /// Raised when game log prints that match has started
    /// <example>InitGame</example>
    /// <value><see cref="MatchStartEvent"/></value>
    /// </summary>
    static event Func<MatchStartEvent, CancellationToken, Task> MatchStarted;
    
    /// <summary>
    /// Raised when game log prints that match has ended
    /// <example>ShutdownGame:</example>
    /// <value><see cref="MatchEndEvent"/></value>
    /// </summary>
    static event Func<MatchEndEvent, CancellationToken, Task> MatchEnded;

    /// <summary>
    /// Raised when game log printed that client has entered the match
    /// <remarks>J;clientNetworkId;clientSlotNumber;clientName</remarks>
    /// <example>J;110000100000000;0;bot</example>
    /// <value><see cref="ClientEnterMatchEvent"/></value>
    /// </summary>
    public static event Func<ClientEnterMatchEvent, CancellationToken, Task> ClientEnteredMatch;
    
    /// <summary>
    /// Raised when game log prints that client has exited the match
    /// <remarks>Q;clientNetworkId;clientSlotNumber;clientName</remarks>
    /// <example>Q;110000100000000;0;bot</example>
    /// <value><see cref="ClientExitMatchEvent"/></value>
    /// </summary>
    static event Func<ClientExitMatchEvent, CancellationToken, Task> ClientExitedMatch;
    
    /// <summary>
    /// Raised when game log prints that client has joined a team
    /// <remarks>JT;clientNetworkId;clientSlotNumber;clientTeam;clientName</remarks>
    /// <example>JT;110000100000000;0;axis;bot</example>
    /// <value><see cref="ClientJoinTeamEvent"/></value>
    /// </summary>
    static event Func<ClientJoinTeamEvent, CancellationToken, Task> ClientJoinedTeam;
    
    /// <summary>
    /// Raised when game log prints that client has been damaged
    /// <remarks>D;victimNetworkId;victimSlotNumber;victimTeam;victimName;attackerNetworkId;attackerSlotNumber;attackerTeam;attackerName;weapon;damage;meansOfDeath;hitLocation</remarks>
    /// <example>D;110000100000000;17;axis;bot_0;110000100000001;4;allies;bot_1;scar_mp;38;MOD_HEAD_SHOT;head</example>
    /// <value><see cref="ClientDamageEvent"/></value>
    /// </summary>
    static event Func<ClientDamageEvent, CancellationToken, Task> ClientDamaged;
    
    /// <summary>
    /// Raised when game log prints that client has been killed
    /// <remarks>K;victimNetworkId;victimSlotNumber;victimTeam;victimName;attackerNetworkId;attackerSlotNumber;attackerTeam;attackerName;weapon;damage;meansOfDeath;hitLocation</remarks>
    /// <example>K;110000100000000;17;axis;bot_0;110000100000001;4;allies;bot_1;scar_mp;100;MOD_HEAD_SHOT;head</example>
    /// <value><see cref="ClientKillEvent"/></value>
    /// </summary>
    static event Func<ClientKillEvent, CancellationToken, Task> ClientKilled;
    
    /// <summary>
    /// Raised when game log prints that client entered a chat message
    /// <remarks>say;clientNetworkId;clientSlotNumber;clientName;message</remarks>
    /// <example>say;110000100000000;0;bot;hello world!</example>
    /// <value><see cref="ClientMessageEvent"/></value>
    /// </summary>
    static event Func<ClientMessageEvent, CancellationToken, Task> ClientMessaged;

    /// <summary>
    /// Raised when game log prints that client entered a command (chat message prefixed with command character(s))
    /// <remarks>say;clientNetworkId;clientSlotNumber;clientName;command</remarks>
    /// <example>say;110000100000000;0;bot;!command</example>
    /// <value><see cref="ClientCommandEvent"/></value>
    /// </summary>
    static event Func<ClientCommandEvent, CancellationToken, Task> ClientEnteredCommand;

    /// <summary>
    /// Raised when game log prints user generated script event
    /// <remarks>GSE;data</remarks>
    /// <example>GSE;loadBank=1</example>
    /// <value><see cref="GameScriptEvent"/></value>
    /// </summary>
    static event Func<GameScriptEvent, CancellationToken, Task> ScriptEventTriggered;

    static Task InvokeEventAsync(CoreEvent coreEvent, CancellationToken token)
    {
        return coreEvent switch
        {
            MatchStartEvent matchStartEvent => MatchStarted?.InvokeAsync(matchStartEvent, token) ?? Task.CompletedTask,
            MatchEndEvent matchEndEvent => MatchEnded?.InvokeAsync(matchEndEvent, token) ?? Task.CompletedTask,
            ClientEnterMatchEvent clientEnterMatchEvent => ClientEnteredMatch?.InvokeAsync(clientEnterMatchEvent, token) ?? Task.CompletedTask,
            ClientExitMatchEvent clientExitMatchEvent => ClientExitedMatch?.InvokeAsync(clientExitMatchEvent, token) ?? Task.CompletedTask,
            ClientJoinTeamEvent clientJoinTeamEvent => ClientJoinedTeam?.InvokeAsync(clientJoinTeamEvent, token) ?? Task.CompletedTask,
            ClientKillEvent clientKillEvent => ClientKilled?.InvokeAsync(clientKillEvent, token) ?? Task.CompletedTask,
            ClientDamageEvent clientDamageEvent => ClientDamaged?.InvokeAsync(clientDamageEvent, token) ?? Task.CompletedTask,
            ClientCommandEvent clientCommandEvent => ClientEnteredCommand?.InvokeAsync(clientCommandEvent, token) ?? Task.CompletedTask,
            ClientMessageEvent clientMessageEvent => ClientMessaged?.InvokeAsync(clientMessageEvent, token) ?? Task.CompletedTask,
            GameScriptEvent gameScriptEvent => ScriptEventTriggered?.InvokeAsync(gameScriptEvent, token) ?? Task.CompletedTask,
            _ => Task.CompletedTask
        };
    }

    static void ClearEventInvocations()
    {
        MatchStarted = null;
        MatchEnded = null;
        ClientEnteredMatch = null;
        ClientExitedMatch = null;
        ClientJoinedTeam = null;
        ClientDamaged = null;
        ClientKilled = null;
        ClientMessaged = null;
        ClientEnteredCommand = null;
        ScriptEventTriggered = null;
    }
}
