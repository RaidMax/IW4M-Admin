using System.Globalization;
using Data.Models.Server;
using Data.Models.Zombie;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using IW4MAdmin.Plugins.ZombieStats.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Game.GameScript;
using SharedLibraryCore.Events.Game.GameScript.Zombie;
using SharedLibraryCore.Interfaces;

namespace ZombieStats.Tests;

public sealed class ParserTests : IDisposable
{
    private const string Player = "12345;2;allies;Player One";
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddTransient<IParserPatternMatcher, ParserPatternMatcher>().BuildServiceProvider();
    private readonly ZombieEventParser _parser = new(NullLogger<ZombieEventParser>.Instance);
    private readonly IW4MAdmin.IW4MServer _server;

    public ParserTests()
    {
        _server = Check.Server(new EFServer { ServerId = 1, Port = 28960 }, gametype: "zclassic");
        var parser = new BaseEventParser(new ParserRegexFactory(_services), NullLogger.Instance,
            new ApplicationConfiguration(), null!);
        parser.Configuration.GuidNumberStyle = NumberStyles.Integer;
        typeof(Server).GetProperty("EventParser")!.SetValue(_server, parser);
    }

    private GameScriptEvent Script(string data) => new()
    {
        ScriptData = data, Owner = _server, GameTime = 120,
        Time = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), Source = GameEvent.EventSource.Log
    };

    [Theory]
    [InlineData("down", typeof(PlayerDownedGameEvent), null, null)]
    [InlineData("perk;buy;juggernaut;2500", typeof(PlayerConsumedPerkGameEvent), "Cost", "2500")]
    [InlineData("perk;buy;juggernaut", typeof(PlayerConsumedPerkGameEvent), "Cost", "0")]
    [InlineData("powerup;grab;max_ammo", typeof(PlayerGrabbedPowerupGameEvent), "PowerupName", "max_ammo")]
    [InlineData("weapon;buy;mp40;1000", typeof(WeaponPurchaseGameEvent), "WeaponName", "mp40")]
    [InlineData("weapon;upgrade;mp40;mp40_upgraded;5000", typeof(PackAPunchGameEvent), "NewWeapon", "mp40_upgraded")]
    [InlineData("weapon;abandon;mp40;5000", typeof(PackAPunchGameEvent), "Outcome", "Abandon")]
    [InlineData("box;take;ray_gun;950", typeof(BoxUseGameEvent), "Outcome", "Take")]
    [InlineData("box;pass;ray_gun;950", typeof(BoxUseGameEvent), "Outcome", "Pass")]
    [InlineData("box;teddy;950", typeof(BoxUseGameEvent), "Outcome", "Teddy")]
    [InlineData("door;buy;750", typeof(DoorPurchaseGameEvent), "Cost", "750")]
    [InlineData("trap;activate;electric;1000", typeof(TrapActivateGameEvent), "TrapType", "electric")]
    [InlineData("build;complete;shield", typeof(BuildCompleteGameEvent), "BuildableName", "shield")]
    [InlineData("gum;activate;perkaholic", typeof(GobbleGumActivatedGameEvent), "GumName", "perkaholic")]
    [InlineData("gum;take;perkaholic;500", typeof(GobbleGumTakenGameEvent), "Cost", "500")]
    [InlineData("gum;take;perkaholic", typeof(GobbleGumTakenGameEvent), "Cost", "0")]
    [InlineData("gum;leave;perkaholic;1500", typeof(GobbleGumAbandonedGameEvent), "Cost", "1500")]
    [InlineData("gum;leave", typeof(GobbleGumAbandonedGameEvent), "GumName", "")]
    [InlineData("bank;deposit;1000", typeof(BankTransactionGameEvent), "IsDeposit", "True")]
    [InlineData("bank;withdraw;1000", typeof(BankTransactionGameEvent), "IsDeposit", "False")]
    [InlineData("bank;deposit", typeof(BankTransactionGameEvent), "Amount", "0")]
    [InlineData("locker;store;ray_gun", typeof(WeaponLockerGameEvent), "IsStore", "True")]
    [InlineData("locker;retrieve;ray_gun", typeof(WeaponLockerGameEvent), "IsStore", "False")]
    [InlineData("locker;retrieve", typeof(WeaponLockerGameEvent), "WeaponName", "")]
    public void Player_actions_preserve_identity_and_payload(string action, Type expectedType, string? property, string? value)
    {
        var source = Script($"GSE;ZP;{Player};{action}");
        var parsed = _parser.ParseScriptEvent(source);
        Assert.NotNull(parsed);
        Assert.IsType(expectedType, parsed);
        Assert.Equal(12345, parsed.Origin.NetworkId);
        Assert.Equal(2, parsed.Origin.ClientNumber);
        Assert.Equal("allies", parsed.Origin.TeamName);
        Assert.Equal("Player One", parsed.Origin.Name);
        Assert.Equal(source.Time, parsed.Time);
        Assert.Equal(source.GameTime, parsed.GameTime);
        Assert.Equal(source.Source, parsed.Source);
        Assert.Same(_server, parsed.Owner);
        if (property != null)
            Assert.Equal(value, Convert.ToString(expectedType.GetProperty(property)!.GetValue(parsed), CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("K", typeof(PlayerKilledGameEvent))]
    [InlineData("D", typeof(PlayerDamageGameEvent))]
    [InlineData("AK", typeof(ZombieKilledGameEvent))]
    [InlineData("AD", typeof(ZombieDamageGameEvent))]
    public void Combat_maps_attacker_and_victim_without_swapping(string kind, Type type)
    {
        var parsed = _parser.ParseScriptEvent(Script($"GSE;{kind};{Player};54321;3;axis;Player Two;mp40;150;MOD_RIFLE_BULLET;head"))!;
        Assert.IsType(type, parsed);
        Assert.Equal(12345, parsed.Target.NetworkId);
        Assert.Equal(54321, parsed.Origin.NetworkId);
        Assert.Equal("Player Two", parsed.Origin.Name);
        Assert.Equal(3, parsed.Origin.ClientNumber);
        Assert.Equal("axis", parsed.Origin.TeamName);
        Assert.Equal("mp40", type.GetProperty("WeaponName")!.GetValue(parsed));
        Assert.Equal(150, type.GetProperty("Damage")!.GetValue(parsed));
        Assert.Equal("MOD_RIFLE_BULLET", type.GetProperty("MeansOfDeath")!.GetValue(parsed));
        Assert.Equal("head", type.GetProperty("HitLocation")!.GetValue(parsed));
    }

    [Fact]
    public void Revive_credits_the_reviver_and_self_revive_uses_the_same_client()
    {
        var revive = Assert.IsType<PlayerRevivedGameEvent>(_parser.ParseScriptEvent(Script($"GSE;ZP;{Player};revive;54321;3;allies;Helper")));
        Assert.Equal(54321, revive.Origin.NetworkId);
        Assert.Equal(12345, revive.Target.NetworkId);
        Assert.False(revive.IsSelfRevive);
        var self = Assert.IsType<PlayerRevivedGameEvent>(_parser.ParseScriptEvent(Script($"GSE;ZP;{Player};revive;self")));
        Assert.True(self.IsSelfRevive);
        Assert.Same(self.Origin, self.Target);
    }

    [Theory]
    [InlineData("1", true)] [InlineData("true", true)] [InlineData("TRUE", true)]
    [InlineData("0", false)] [InlineData("false", false)]
    public void Round_data_accepts_game_boolean_renderings(string gameOver, bool expected)
    {
        var parsed = Assert.IsType<PlayerRoundDataGameEvent>(_parser.ParseScriptEvent(Script($"GSE;RD;{Player};2000;500;6;{gameOver}")));
        Assert.Equal(expected, parsed.IsGameOver);
        Assert.Equal(2000, parsed.TotalScore);
        Assert.Equal(500, parsed.CurrentScore);
        Assert.Equal(6, parsed.CurrentRound);
    }

    [Theory]
    [InlineData("GSE;RC;7", typeof(RoundEndEvent), "RoundNumber", "7")]
    [InlineData("GSE;ZW;zombies;7;100;24", typeof(ZombiesRemainingGameEvent), "Remaining", "100")]
    [InlineData("GSE;ZW;easter_egg;step;t4_vr_radio_1", typeof(EasterEggStepGameEvent), "StepKey", "t4_vr_radio_1")]
    [InlineData("GSE;ZW;easter_egg;COMPLETE;zm_moon", typeof(EasterEggCompleteGameEvent), "MapName", "zm_moon")]
    public void World_events_decode_the_wire_contract(string data, Type type, string property, string expected)
    {
        var parsed = _parser.ParseScriptEvent(Script(data));
        Assert.IsType(type, parsed);
        Assert.Equal(expected, Convert.ToString(type.GetProperty(property)!.GetValue(parsed), CultureInfo.InvariantCulture));
        Assert.Null(parsed!.Origin);
    }

    [Theory]
    [InlineData("world", "on", PowerSource.World, PowerState.On)]
    [InlineData("player", "off", PowerSource.Player, PowerState.Off)]
    public void Power_attribution_is_distinct_from_log_provenance(string source, string state, PowerSource expectedSource, PowerState expectedState)
    {
        var parsed = Assert.IsType<PowerStateChangeGameEvent>(_parser.ParseScriptEvent(Script($"GSE;ZW;power;{state};{source};{Player}")));
        Assert.Equal(expectedSource, parsed.Source);
        Assert.Equal(expectedState, parsed.State);
        Assert.Equal(GameEvent.EventSource.Log, ((GameEvent)parsed).Source);
        if (source == "player") Assert.Equal(12345, parsed.Origin.NetworkId);
        else Assert.Null(parsed.Origin);
    }

    [Theory]
    [InlineData("dog", ZombieSpecialRoundType.Dog)] [InlineData("monkey", ZombieSpecialRoundType.Monkey)]
    [InlineData("leaper", ZombieSpecialRoundType.Leaper)] [InlineData("thief", ZombieSpecialRoundType.Thief)]
    [InlineData("wasp", ZombieSpecialRoundType.Wasp)] [InlineData("spider", ZombieSpecialRoundType.Spider)]
    [InlineData("robot", ZombieSpecialRoundType.Robot)] [InlineData("quad", ZombieSpecialRoundType.Quad)]
    [InlineData("boss", ZombieSpecialRoundType.Boss)] [InlineData("ee", ZombieSpecialRoundType.Ee)]
    [InlineData("future_kind", null)]
    public void Special_round_tokens_are_forward_compatible(string token, ZombieSpecialRoundType? expected)
    {
        var parsed = Assert.IsType<RoundSpecialGameEvent>(_parser.ParseScriptEvent(Script($"GSE;ZW;round_special;6;{token}")));
        Assert.Equal(6, parsed.RoundNumber);
        Assert.Equal(expected, parsed.SpecialType);
        if (expected.HasValue) Assert.Equal(token, expected.Value.ToToken());
    }

    [Theory]
    [InlineData("")] [InlineData("GSE")] [InlineData("latency;123")] [InlineData("GSE;unknown")]
    [InlineData("GSE;AK")] [InlineData("GSE;D;12345")]
    [InlineData("GSE;RC;abc")] [InlineData("GSE;RC;2147483648")]
    [InlineData("GSE;RD;12345;2;allies;Name;abc;500;6;0")]
    [InlineData("GSE;ZW;unknown")] [InlineData("GSE;ZW;easter_egg;unknown;zm_moon")]
    [InlineData("GSE;ZW;easter_egg;step")] [InlineData("GSE;ZW;zombies;6;2")]
    [InlineData("GSE;ZW;power;bad;world")] [InlineData("GSE;ZW;power;on;bad")]
    [InlineData("GSE;ZW;power;on;player")]
    [InlineData("GSE;ZP;12345;bad;allies;Name;down")]
    [InlineData("GSE;ZP;12345;2;allies;Name;unknown")]
    [InlineData("GSE;ZP;12345;2;allies;Name;revive")]
    [InlineData("GSE;ZP;12345;2;allies;Name;weapon;bad")]
    [InlineData("GSE;ZP;12345;2;allies;Name;box;bad")]
    [InlineData("GSE;ZP;12345;2;allies;Name;bank;bad")]
    [InlineData("GSE;ZP;12345;2;allies;Name;locker;bad")]
    [InlineData("GSE;ZP;12345;2;allies;Name;gum;bad")]
    public void Corrupt_or_unrelated_lines_are_dropped_without_poisoning_the_next_event(string data)
    {
        Assert.Null(_parser.ParseScriptEvent(Script(data)));
        Assert.IsType<RoundEndEvent>(_parser.ParseScriptEvent(Script("GSE;RC;8")));
    }

    [Fact] public Task Log_clock_and_disposal_follow_the_originating_server() => ZombieLifecycleChecks.Run();

    [Fact]
    public void Client_guids_follow_the_game_parsers_number_style()
    {
        _server.EventParser.Configuration.GuidNumberStyle = NumberStyles.HexNumber;
        var parsed = Assert.IsType<PlayerDownedGameEvent>(_parser.ParseScriptEvent(Script($"GSE;ZP;{Player};down")));
        Assert.Equal(0x12345, parsed.Origin.NetworkId);
    }

    public void Dispose() => _services.Dispose();
}
