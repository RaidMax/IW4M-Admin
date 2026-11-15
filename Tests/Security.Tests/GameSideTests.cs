using System.Globalization;
using Data.Models.Server;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using IW4MAdmin.Plugins.ZombieStats.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Security.Tests.Infrastructure;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Game.GameScript.Zombie;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using EFClient = SharedLibraryCore.Database.Models.EFClient;
using Permission = Data.Models.Client.EFClient.Permission;

namespace Security.Tests;

/// <summary>
/// S4 (a player with no account: chooses a name and chat text), S5 (the in-game command gate) and
/// S7 (what the audit log keeps). Parsers and the gate are fed crafted input in-process.
/// </summary>
public class GameSideTests
{
    private static readonly SharedLibraryCore.Localization.TranslationLookup Translations = new() { Set = new Dictionary<string, string>() };

    private static BaseEventParser NewParser()
    {
        var services = new ServiceCollection().AddTransient<IParserPatternMatcher, ParserPatternMatcher>()
            .BuildServiceProvider();
        var parser = new BaseEventParser(new ParserRegexFactory(services), NullLogger.Instance,
            new ApplicationConfiguration(), null!);
        parser.Configuration.GuidNumberStyle = NumberStyles.Integer;
        return parser;
    }

    // ---- S4: chat text ----

    [Theory]
    [InlineData("K;777;3;allies;Victim;12345;2;axis;Player;mp40_mp;100;MOD_RIFLE_BULLET;head")]
    [InlineData("Q;777;3;Victim")]
    [InlineData("J;999;5;Forged")]
    [InlineData("GSE;ZP;777;3;allies;Victim;down")]
    public void Chat_text_shaped_like_another_log_line_is_only_ever_a_chat_message(string chat)
    {
        var parsed = NewParser().GenerateGameEvent($" 10:00 say;12345;2;Player;{chat}");

        Assert.Equal(GameEvent.EventType.Say, parsed.Type);
        Assert.Equal(12345, parsed.Origin.NetworkId);
        Assert.Equal(chat, parsed.Data);
    }

    [Fact]
    public void Control_characters_in_chat_text_are_dropped()
    {
        var parsed = NewParser().GenerateGameEvent(" 10:00 say;12345;2;Player;hello\u0015 \u0001world\u007f");

        Assert.Equal("hello world", parsed.Data);
    }

    // ---- S4: player name in the zombie GSE wire format ----

    /// <summary>
    /// The GSC side writes "GSE;ZP;{guid};{slot};{team};{name};{action...}" with the name unescaped
    /// (GameFiles/ZombieStats/_zm_stats_t6.gsc BuildPlayerInfoString) and the parser finds the action by position.
    /// A player whose name contains the field separator must not be able to choose what event is recorded.
    /// </summary>
    [Fact]
    public void Zombie_event_is_not_redefined_by_a_separator_in_the_player_name()
    {
        var server = Check.Server(new EFServer { ServerId = 1, Port = 28960 }, gametype: "zclassic");
        typeof(Server).GetProperty("EventParser")!.SetValue(server, NewParser());
        var parser = new ZombieEventParser(NullLogger<ZombieEventParser>.Instance);

        const string name = "Evil;powerup;grab;max_ammo";
        // what the game writes when this player goes down
        var parsed = parser.ParseScriptEvent(new GameScriptEvent
        {
            ScriptData = $"GSE;ZP;12345;2;allies;{name};down", Owner = server, GameTime = 120,
            Time = DateTime.UtcNow, Source = GameEvent.EventSource.Log
        });

        Assert.True(parsed is null or PlayerDownedGameEvent,
            $"a 'down' line for a player named '{name}' was recorded as {parsed?.GetType().Name}");
    }

    // ---- S5: command gate (SharedLibraryCore/Commands/CommandProcessing.cs) ----

    private static (ManagerHarness Harness, Server Server) GateHarness(ClientService? clients = null)
    {
        Utilities.CurrentLocalization = new SharedLibraryCore.Localization.Layout(new Dictionary<string, string>());
        var harness = new ManagerHarness(clients);
        var server = harness.AddServer();
        var config = new CommandConfiguration();
        harness.Commands.Add(new UnbanCommand(config, Translations));
        harness.Commands.Add(new BanCommand(new ApplicationConfiguration(), config, Translations));
        harness.Commands.Add(new RequestTokenCommand(config, Translations));
        harness.Commands.Add(new SetPasswordCommand(config, Translations));
        return (harness, server);
    }

    private static GameEvent CommandEvent(Server server, Permission level, string text, EFClient? impersonator = null) => new()
    {
        Type = GameEvent.EventType.Command,
        Data = text,
        Owner = server,
        Origin = new EFClient { ClientId = 20, Level = level, CurrentServer = server },
        ImpersonationOrigin = impersonator
    };

    [Theory]
    [InlineData(Permission.User, "!unban @5 reason")]
    [InlineData(Permission.Trusted, "!unban @5 reason")]
    [InlineData(Permission.Administrator, "!unban @5 reason")]
    [InlineData(Permission.Administrator, "!ban @5 reason")]
    [InlineData(Permission.User, "!requesttoken")]
    [InlineData(Permission.Flagged, "!rt")]
    [InlineData(Permission.Trusted, "!setpassword hunter22")]
    public async Task Command_gate_refuses_a_client_below_the_command_level(Permission level, string text)
    {
        var (_, server) = GateHarness();
        var config = new ApplicationConfiguration();

        await Assert.ThrowsAsync<CommandException>(() =>
            CommandProcessing.ValidateCommand(CommandEvent(server, level, text), config, new CommandConfiguration()));
    }

    [Fact]
    public async Task Command_gate_admits_a_client_at_the_command_level()
    {
        var (_, server) = GateHarness();

        var command = await CommandProcessing.ValidateCommand(
            CommandEvent(server, Permission.Trusted, "!requesttoken"), new ApplicationConfiguration(), new CommandConfiguration());

        Assert.IsType<RequestTokenCommand>(command);
    }

    [Fact]
    public async Task Command_gate_refuses_impersonation_of_a_command_that_does_not_allow_it()
    {
        var (_, server) = GateHarness();
        var owner = new EFClient { ClientId = 1, Level = Permission.Owner, CurrentServer = server };

        // `!runas` re-dispatches the inner command with Origin = target and ImpersonationOrigin = the caller
        await Assert.ThrowsAsync<CommandException>(() => CommandProcessing.ValidateCommand(
            CommandEvent(server, Permission.SeniorAdmin, "!requesttoken", owner), new ApplicationConfiguration(),
            new CommandConfiguration()));
    }

    // ---- S7: audit log content (SharedLibraryCore/Services/ChangeHistoryService.cs) ----

    private static async Task<string> RecordedCommandTextAsync(Command command, string message)
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new ChangeHistoryService(NullLogger<ChangeHistoryService>.Instance, database.Factory);
        await service.Add(new GameEvent
        {
            Type = GameEvent.EventType.Command,
            Extra = command,
            Message = message,
            Data = message,
            Origin = new EFClient { ClientId = 4 },
            Target = new EFClient { ClientId = 5 }
        });

        await using var context = database.Factory.CreateContext();
        return (await context.EFChangeHistory.SingleAsync()).CurrentValue;
    }

    [Fact]
    public async Task Audit_log_masks_the_password_of_a_setpassword_command()
    {
        var recorded = await RecordedCommandTextAsync(new SetPasswordCommand(new CommandConfiguration(), Translations),
            "!setpassword hunter22");

        Assert.DoesNotContain("hunter22", recorded);
    }

    /// <summary>
    /// SetPasswordCommand has AllowImpersonation = true, so "!runas {target} setpassword {password}" is a
    /// supported way to set another client's webfront password. The outer runas event is audited too.
    /// </summary>
    [Fact]
    public async Task Audit_log_masks_a_password_set_through_runas()
    {
        var recorded = await RecordedCommandTextAsync(new RunAsCommand(new CommandConfiguration(), Translations),
            "!runas @5 setpassword hunter22");

        Assert.True(!recorded.Contains("hunter22"),
            $"the audit row for a runas-wrapped setpassword stored the new password in clear: '{recorded}'");
    }
}
