using System.Reflection;
using Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using IW4MAdmin.Plugins.ZombieStats.Events;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Game.GameScript.Zombie;

internal static class ZombieLifecycleChecks
{
    internal static async Task Run()
    {
        var original = Check.Server(new Data.Models.Server.EFServer { ServerId = 301, Port = 301 }, gametype: "zclassic");
        var destination = Check.Server(new Data.Models.Server.EFServer { ServerId = 302, Port = 302 }, gametype: "dm");
        var client = new SharedLibraryCore.Database.Models.EFClient
        {
            NetworkId = 1234,
            GameName = Reference.Game.T5, CurrentServer = destination
        };
        var enhancer = DispatchProxy.Create<IZombieStatsEnhancer, CapturingEnhancer>();
        using var services = new ServiceCollection().AddSingleton(enhancer).BuildServiceProvider();
        var plugin = new IW4MAdmin.Plugins.ZombieStats.Plugin(
            NullLogger<IW4MAdmin.Plugins.ZombieStats.Plugin>.Instance,
            new IW4MAdmin.Plugins.ZombieStats.Events.ZombieEventParser(
                NullLogger<IW4MAdmin.Plugins.ZombieStats.Events.ZombieEventParser>.Instance), null!, services);
        await (Task)plugin.GetType().GetMethod("OnClientDisposed", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(plugin, [new ClientStateDisposeEvent { Source = original, Client = client }, CancellationToken.None])!;
        Check.Equal(301L, ((CapturingEnhancer)(object)enhancer).DisposedServer,
            "Queued dispose uses event source after client's CurrentServer changes");

        using var parserServices = new ServiceCollection()
            .AddTransient<IParserPatternMatcher, ParserPatternMatcher>().BuildServiceProvider();
        var baseParser = new BaseEventParser(new ParserRegexFactory(parserServices), NullLogger.Instance,
            new ApplicationConfiguration(), null!);
        var zombieParser = new ZombieEventParser(NullLogger<ZombieEventParser>.Instance);
        var script = (GameScriptEvent)baseParser.GenerateGameEvent(" 10:00 GSE;RC;2");
        script.Owner = original;
        var parsed = zombieParser.ParseScriptEvent(script)!;
        Check.Equal((long?)600, parsed.GameTime, "Zombies parsing preserves elapsed log seconds");
        Check.Equal(script.Time, parsed.Time, "Zombies parsing retains source observation time");
        Check.Equal(GameEvent.EventSource.Log, parsed.Source, "Zombies parsing retains log provenance");
        Check.Equal(original, parsed.Owner, "Zombies parsing retains the originating server");
        Check.Equal((long?)0, zombieParser.ParseScriptEvent(
                (GameScriptEvent)baseParser.GenerateGameEvent(" 0:00 GSE;RC;2"))!.GameTime,
            "Log time zero remains a usable round boundary");
        Check.Equal((long?)null, zombieParser.ParseScriptEvent(
                (GameScriptEvent)baseParser.GenerateGameEvent("GSE;RC;2"))!.GameTime,
            "Untimestamped log lines do not invent a zero-second game clock");
        var power = (PowerStateChangeGameEvent)zombieParser.ParseScriptEvent(
            (GameScriptEvent)baseParser.GenerateGameEvent(" 12:00 GSE;ZW;power;on;world"))!;
        Check.Equal(PowerSource.World, power.Source, "Log provenance preserves the distinct power attribution field");
    }
}

public class CapturingEnhancer : DispatchProxy
{
    public long DisposedServer { get; private set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "OnClientDisposed") DisposedServer = ((IGameServer)args![1]!).LegacyDatabaseId;
        return method.ReturnType == typeof(Task) ? Task.CompletedTask : null;
    }
}
