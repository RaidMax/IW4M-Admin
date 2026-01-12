using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Server;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.RConParsers;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application;

/// <summary>
/// A lightweight server implementation for running without real game servers.
/// Inherits all database operations from IW4MServer but stubs RCon-specific methods.
/// </summary>
public class DummyServer(
    ServerConfiguration serverConfiguration,
    CommandConfiguration commandConfiguration,
    ITranslationLookup lookup,
    IMetaServiceV2 metaService,
    IServiceProvider serviceProvider,
    IClientNoticeMessageFormatter messageFormatter,
    ILookupCache<EFServer> serverCache,
    IServerStateChecker stateChecker)
    : IW4MServer(serverConfiguration, commandConfiguration, lookup, metaService,
        serviceProvider, messageFormatter, serverCache, stateChecker)
{
    public new async Task Initialize()
    {
        GameName = Game.UKN;
        Hostname = "HOST";
        MaxClients = 0;
        CurrentMap = new Map { Name = "HOST", Alias = "HOST" };
        ResolvedIpEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);
        RconParser = ActivatorUtilities.CreateInstance<DynamicRConParser>(serviceProvider);
        EventParser = ActivatorUtilities.CreateInstance<DynamicEventParser>(serviceProvider);

        await Task.CompletedTask;
    }

    public override Task<string[]> ExecuteCommandAsync(string command, CancellationToken token = default)
    {
        return Task.FromResult(Array.Empty<string>());
    }

    public override Task SetDvarAsync(string name, object value, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public override Task<bool> ProcessUpdatesAsync(CancellationToken token)
    {
        return Task.FromResult(true);
    }

    public override Task<long> GetIdForServer(Server server = null)
    {
        return Task.FromResult(0L);
    }

    public override long LegacyDatabaseId => 0;
}
