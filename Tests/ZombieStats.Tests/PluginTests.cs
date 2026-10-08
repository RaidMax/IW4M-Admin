using System.Globalization;
using System.Reflection;
using Data.Models.Client.Stats;
using Data.Models.Server;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using IW4MAdmin.Plugins.ZombieStats.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Game.GameScript.Zombie;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using ZombiePlugin = IW4MAdmin.Plugins.ZombieStats.Plugin;

namespace ZombieStats.Tests;

public sealed class PluginTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly ZombiePlugin _plugin;
    private readonly IZombieStatsEnhancer _enhancer = DispatchProxy.Create<IZombieStatsEnhancer, EnhancerSpy>();
    private readonly IManager _manager = DispatchProxy.Create<IManager, ManagerSpy>();
    private readonly IW4MAdmin.IW4MServer _server = Check.Server(new EFServer { ServerId = 1, Port = 28960 }, gametype: "zclassic");
    private EnhancerSpy Enhancer => (EnhancerSpy)(object)_enhancer;
    private ManagerSpy Manager => (ManagerSpy)(object)_manager;

    public PluginTests()
    {
        _services = new ServiceCollection().AddTransient<IParserPatternMatcher, ParserPatternMatcher>()
            .AddSingleton(_enhancer).BuildServiceProvider();
        var parser = new BaseEventParser(new ParserRegexFactory(_services), NullLogger.Instance, new ApplicationConfiguration(), null!);
        parser.Configuration.GuidNumberStyle = NumberStyles.Integer;
        typeof(Server).GetProperty("EventParser")!.SetValue(_server, parser);
        typeof(Server).GetProperty("Manager")!.SetValue(_server, _manager);
        _plugin = new ZombiePlugin(NullLogger<ZombiePlugin>.Instance,
            new ZombieEventParser(NullLogger<ZombieEventParser>.Instance), null!, _services);
    }

    private Task Handle(string method, object gameEvent, CancellationToken token = default) => (Task)typeof(ZombiePlugin)
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(_plugin, [gameEvent, token])!;
    private EFClient Connect(long id) { var client = new EFClient { NetworkId = id, CurrentServer = _server }; _server.Clients.Add(client); return client; }
    private Task Script(string wire) => Handle("OnScriptEvent", new GameScriptEvent
        { Owner = _server, ScriptData = wire, GameTime = 120, Source = GameEvent.EventSource.Log });

    [Theory]
    [InlineData("AK", typeof(ClientKillEvent), false)]
    [InlineData("AD", typeof(ClientDamageEvent), false)]
    [InlineData("K", typeof(ClientKillEvent), true)]
    public async Task Combat_bridge_resolves_connected_clients_and_preserves_log_data(string kind, Type bridgedType, bool playerIsVictim)
    {
        var player = Connect(playerIsVictim ? 12345 : 54321);
        await Script($"GSE;{kind};12345;2;allies;Victim;54321;3;axis;Attacker;mp40;100;MOD_MELEE;torso");
        var bridged = Assert.Single(Manager.Queued);
        Assert.IsType(bridgedType, bridged);
        Assert.Same(player, playerIsVictim ? bridged.Target : bridged.Origin);
        Assert.Equal("Zombie", (playerIsVictim ? bridged.Origin : bridged.Target).Name);
        Assert.Equal(120, bridged.GameTime);
        Assert.Equal(GameEvent.EventSource.Log, bridged.Source);
        Assert.Same(_server, bridged.Owner);
        Assert.StartsWith(playerIsVictim ? "K;" : kind[1..] + ";", bridged.Data);
        Assert.Equal(new[] { "ProcessEventAsync", "UpdateState" }, Enhancer.Calls.Select(c => c.Name));
    }

    [Fact]
    public async Task A_handler_stuck_behind_premium_maintenance_frees_its_slot_when_the_host_times_it_out()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enhancer.PendingProcess = gate.Task;
        using var hostTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var handler = Handle("OnScriptEvent", new GameScriptEvent
            { Owner = _server, ScriptData = "GSE;RC;4", GameTime = 120, Source = GameEvent.EventSource.Log }, hostTimeout.Token);

        // Returns once the host gives up, instead of holding the event slot until maintenance ends.
        Assert.Same(handler, await Task.WhenAny(handler, Task.Delay(TimeSpan.FromSeconds(10))));
        await handler;
        Assert.Equal(new[] { "ProcessEventAsync" }, Enhancer.Calls.Select(c => c.Name));

        // The abandoned call still completes in the background.
        gate.SetResult();
        await gate.Task;
    }

    [Fact]
    public async Task Disconnected_attackers_do_not_receive_bridged_stats()
    {
        await Script("GSE;AK;-1;0;axis;Zombie;54321;3;allies;Gone;mp40;100;MOD_MELEE;torso");
        Assert.Empty(Manager.Queued);
        Assert.Contains(Enhancer.Calls, c => c.Name == "ProcessEventAsync");
    }

    [Fact]
    public async Task Unknown_multiplayer_servers_are_ignored_but_known_zombie_servers_survive_stale_gametype()
    {
        typeof(Server).GetProperty("Gametype")!.SetValue(_server, "dm");
        await Script("GSE;RC;2");
        Assert.Empty(Enhancer.Calls);
        typeof(Server).GetProperty("Gametype")!.SetValue(_server, "zclassic");
        await Script("GSE;RC;2");
        typeof(Server).GetProperty("Gametype")!.SetValue(_server, "dm");
        await Script("GSE;RC;3");
        Assert.Equal(3, _server.ZombieRoundNumber);
        Assert.Equal(2, Enhancer.Calls.Count(c => c.Name == "ProcessEventAsync"));
        typeof(Server).GetProperty("Gametype")!.SetValue(_server, "zclassic");
        await Script("GSE;RC;4");
        Assert.Equal(4, _server.ZombieRoundNumber);
    }

    [Fact]
    public async Task Malformed_script_does_not_forward_or_flush_and_round_data_updates_the_display()
    {
        await Script("GSE;RC;bad");
        Assert.Empty(Enhancer.Calls);
        Assert.Null(_server.ZombieRoundNumber);
        await Script("GSE;RD;12345;2;allies;Player;1000;500;6;false");
        Assert.Equal(6, _server.ZombieRoundNumber);
    }

    [Fact]
    public async Task Authorization_installs_zombie_skill_and_neutral_elo()
    {
        var client = Connect(12345);
        await Handle("OnClientAuthorized", new ClientStateAuthorizeEvent { Client = client, Source = _server });
        var skill = client.GetAdditionalProperty<Func<Data.Models.Client.EFClient, EFClientStatistics, double>>("SkillFunction");
        var elo = client.GetAdditionalProperty<Func<EFClient, EFClientStatistics, double>>("EloRatingFunction");
        Assert.Equal(42, skill(client, new EFClientStatistics()));
        Assert.Equal(1, elo(client, new EFClientStatistics()));
        Assert.Equal(new[] { "GetSkillCalculation", "OnClientAuthorized", "UpdateState" }, Enhancer.Calls.Select(c => c.Name));
    }

    [Fact]
    public async Task Empty_match_start_clears_old_round_display_before_roster_guard()
    {
        _server.ZombieRoundNumber = 30;
        await Handle("OnMatchStarted", new MatchStartEvent { Owner = _server });
        Assert.Null(_server.ZombieRoundNumber);
        Assert.Empty(Enhancer.Calls);
        Connect(12345);
        await Handle("OnMatchStarted", new MatchStartEvent { Owner = _server });
        Assert.Equal(new[] { "OnMatchStarted", "UpdateState" }, Enhancer.Calls.Select(c => c.Name));
    }

    [Fact]
    public async Task Heartbeat_reconciles_clients_and_match_end_flushes()
    {
        await Handle("OnClientDataUpdated", new ClientDataUpdateEvent { Server = _server, Clients = [] });
        _server.ZombieRoundNumber = 20;
        await Handle("OnMatchEnded", new MatchEndEvent { Owner = _server });
        Assert.Null(_server.ZombieRoundNumber);
        Assert.Equal(new[] { "ReconcileConnectedClients", "OnMatchEnded", "UpdateState" }, Enhancer.Calls.Select(c => c.Name));
    }

    [Fact]
    public async Task Unload_flush_failure_is_contained()
    {
        Enhancer.FailFlush = true;
        await Handle("OnUnload", _manager);
        Assert.Equal("UpdateState", Assert.Single(Enhancer.Calls).Name);
    }

    [Fact]
    public async Task Load_initializes_premium_and_registers_metrics_and_typed_stat_transformer()
    {
        await Handle("OnLoad", _manager);
        Assert.Equal("Initialize", Assert.Single(Enhancer.Calls).Name);
        var metrics = Assert.Single(Manager.Metrics);
        var transform = Assert.Single(Manager.Transformers);
        await metrics([], 1, "zombies", true);
        await transform([], 1, "zombies");
        Assert.Equal(new[] { "Initialize", "GetTopStatsMetrics", "TransformTopStats" }, Enhancer.Calls.Select(c => c.Name));
    }

    [Fact]
    public async Task Free_plugin_runs_without_premium_and_preserves_existing_skill()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var free = new ZombiePlugin(NullLogger<ZombiePlugin>.Instance,
            new ZombieEventParser(NullLogger<ZombieEventParser>.Instance), null!, services);
        try
        {
            Task Call(string method, object data) => (Task)typeof(ZombiePlugin)
                .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(free, [data, CancellationToken.None])!;
            var client = Connect(12345);
            await Call("OnClientAuthorized", new ClientStateAuthorizeEvent { Client = client, Source = _server });
            var skill = client.GetAdditionalProperty<Func<Data.Models.Client.EFClient, EFClientStatistics, double>>("SkillFunction");
            Assert.Equal(7.5, skill(client, new EFClientStatistics { Skill = 7.5 }));
            await Call("OnLoad", _manager);
            Assert.Empty(Manager.Transformers);
            var metrics = Assert.Single(Manager.Metrics);
            await metrics([], 1, "zombies", false);
            await metrics(new Dictionary<int, List<Data.Models.EFMeta>> { [1] = [] }, null, "zombies", false);
            await metrics(new Dictionary<int, List<Data.Models.EFMeta>> { [1] = [] }, 1, "zombies", true);
            await Call("OnUnload", _manager);
        }
        finally { RemoveHandlers(free); }
    }

    public void Dispose()
    {
        RemoveHandlers(_plugin);
        _services.Dispose();
    }

    private static void RemoveHandlers(ZombiePlugin plugin)
    {
        // Remove only this fixture's handlers; never clear subscriptions belonging to another fixture.
        foreach (var type in new[] { typeof(IManagementEventSubscriptions), typeof(IGameServerEventSubscriptions), typeof(IGameEventSubscriptions) })
        foreach (var evt in type.GetEvents(BindingFlags.Public | BindingFlags.Static))
        {
            var handlerName = evt.Name switch
            {
                "Load" => "OnLoad", "Unload" => "OnUnload", "ClientStateAuthorized" => "OnClientAuthorized",
                "ClientStateDisposed" => "OnClientDisposed", "ClientDataUpdated" => "OnClientDataUpdated",
                "ScriptEventTriggered" => "OnScriptEvent", "MatchEnded" => "OnMatchEnded", "MatchStarted" => "OnMatchStarted",
                _ => null
            };
            if (handlerName == null) continue;
            var method = typeof(ZombiePlugin).GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Instance)!;
            evt.RemoveEventHandler(null, method.CreateDelegate(evt.EventHandlerType!, plugin));
        }
    }
}

public class EnhancerSpy : DispatchProxy
{
    public List<(string Name, object?[] Args)> Calls { get; } = [];
    public bool FailFlush { get; set; }
    public Task? PendingProcess { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        Calls.Add((method!.Name, args ?? []));
        if (method.Name == "ProcessEventAsync" && PendingProcess is not null) return PendingProcess;
        if (method.Name == "GetSkillCalculation") return (Func<Data.Models.Client.EFClient, EFClientStatistics, double>)((_, _) => 42);
        if (method.Name == "UpdateState" && FailFlush) return Task.FromException(new InvalidOperationException("write failed"));
        return method.ReturnType == typeof(Task) ? Task.CompletedTask : null;
    }
}

public class ManagerSpy : DispatchProxy
{
    public List<GameEvent> Queued { get; } = [];
    public List<Func<Dictionary<int, List<Data.Models.EFMeta>>, long?, string, bool, Task>> Metrics { get; } = [];
    public List<Func<IList<ITopStatsMutable>, long?, string, Task>> Transformers { get; } = [];
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "QueueEvent") Queued.Add((GameEvent)args![0]!);
        if (method.Name == "get_CustomStatsMetrics") return Metrics;
        if (method.Name == "get_CustomTopStatsTransformers") return Transformers;
        return null;
    }
}
