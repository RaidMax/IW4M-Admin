using System.Reflection;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;

namespace Security.Tests.Infrastructure;

/// <summary>
/// Reflection proxy that stands in for any host interface. Unconfigured members return a default
/// (null, zero, or a completed task). Nothing it does touches a network or a real server.
/// </summary>
public class Fake<T> : DispatchProxy where T : class
{
    public Dictionary<string, Func<object?[], object?>> Handlers { get; } = new();
    public List<(string Method, object?[] Args)> Calls { get; } = [];

    public static (T Proxy, Fake<T> Control) Create()
    {
        var proxy = Create<T, Fake<T>>();
        return (proxy, (Fake<T>)(object)proxy);
    }

    public Fake<T> On(string method, Func<object?[], object?> handler)
    {
        Handlers[method] = handler;
        return this;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        args ??= [];
        Calls.Add((targetMethod!.Name, args));
        if (Handlers.TryGetValue(targetMethod.Name, out var handler))
        {
            return handler(args);
        }

        return DefaultFor(targetMethod.ReturnType);
    }

    private static object? DefaultFor(Type type)
    {
        if (type == typeof(void)) return null;
        if (type == typeof(Task)) return Task.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = type.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner)
                .Invoke(null, [inner.IsValueType ? Activator.CreateInstance(inner) : null]);
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

/// <summary>An IManager that records events instead of dispatching them.</summary>
public sealed class ManagerHarness
{
    public IManager Manager { get; }
    public Fake<IManager> Control { get; }
    public List<GameEvent> GameEvents { get; } = [];
    public List<CoreEvent> CoreEvents { get; } = [];
    public ApplicationConfiguration Configuration { get; } = new();
    public List<Server> Servers { get; } = [];
    public List<IManagerCommand> Commands { get; } = [];
    public System.Collections.Concurrent.ConcurrentDictionary<long, GameEvent> ProcessingEvents { get; } = new();
    private EventHandler<GameEvent>? _executed;

    public ManagerHarness(ClientService? clientService = null, ITokenAuthentication? tokens = null)
    {
        Utilities.DefaultLogger ??= NullLogger.Instance;
        (Manager, Control) = Fake<IManager>.Create();
        var (configHandler, configControl) = Fake<IConfigurationHandler<ApplicationConfiguration>>.Create();
        configControl.On("Configuration", _ => Configuration);

        Control.On("AddEvent", args =>
        {
            var gameEvent = (GameEvent)args[0]!;
            GameEvents.Add(gameEvent);
            gameEvent.Complete();
            return null;
        });
        Control.On("QueueEvent", args =>
        {
            CoreEvents.Add((CoreEvent)args[0]!);
            return null;
        });
        Control.On("add_OnGameEventExecuted", args =>
        {
            _executed += (EventHandler<GameEvent>)args[0]!;
            return null;
        });
        Control.On("remove_OnGameEventExecuted", args =>
        {
            _executed -= (EventHandler<GameEvent>)args[0]!;
            return null;
        });
        Control.On("GetApplicationSettings", _ => configHandler);
        Control.On("get_Servers", _ => Servers);
        Control.On("GetServers", _ => Servers);
        Control.On("GetClientService", _ => clientService);
        Control.On("get_TokenAuthenticator", _ => tokens);
        Control.On("GetActiveClients", _ => new List<SharedLibraryCore.Database.Models.EFClient>());
        Control.On("FindActiveClient", args => args[0]);
        Control.On("get_ProcessingEvents", _ => ProcessingEvents);
        Control.On("get_Commands", _ => Commands);
    }

    /// <summary>Raises the same notification the host raises after a game event has been processed.</summary>
    public void RaiseExecuted(GameEvent gameEvent) => _executed?.Invoke(Manager, gameEvent);

    public Server AddServer()
    {
        var server = Check.Server(new Data.Models.Server.EFServer { Port = 28960 });
        typeof(Server).GetProperty(nameof(Server.Manager))!.SetValue(server, Manager);
        typeof(Server).GetProperty(nameof(Server.RconParser))!.SetValue(server, Fake<IRConParser>.Create().Proxy);
        Servers.Add(server);
        return server;
    }
}

/// <summary>Disposable, fully migrated SQLite database that lives only in this process's memory.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public IDatabaseContextFactory Factory { get; private set; } = null!;
    public ClientService Clients { get; private set; } = null!;
    public ApplicationConfiguration Configuration { get; } = new();

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        await database._connection.OpenAsync();
        database.Factory = new Factory(new DbContextOptionsBuilder().UseSqlite(database._connection).Options);
        await using var context = database.Factory.CreateContext();
        await context.Database.MigrateAsync();
        database.Clients = new ClientService(NullLogger<ClientService>.Instance, database.Factory,
            database.Configuration, null!);
        return database;
    }

    /// <summary>Adds a fixture client. Returns its client id.</summary>
    public async Task<int> AddClientAsync(EFClient.Permission level, string name, string? password = null,
        string? twoFactorSecret = null)
    {
        await using var context = Factory.CreateContext();
        var link = new EFAliasLink();
        var client = new EFClient
        {
            NetworkId = Random.Shared.NextInt64(1, long.MaxValue),
            GameName = Reference.Game.IW4,
            Level = level,
            Active = true,
            FirstConnection = DateTime.UtcNow,
            LastConnection = DateTime.UtcNow,
            AliasLink = link,
            CurrentAlias = new EFAlias { Name = name, SearchableName = name.ToLowerInvariant(), Link = link, DateAdded = DateTime.UtcNow },
            TwoFactorSecret = twoFactorSecret
        };

        if (password is not null)
        {
            var hashed = SharedLibraryCore.Helpers.Hashing.Hash(password);
            client.Password = hashed[0];
            client.PasswordSalt = hashed[1];
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return client.ClientId;
    }

    public async Task SetLevelAsync(int clientId, EFClient.Permission level)
    {
        await using var context = Factory.CreateContext();
        var client = await context.Clients.SingleAsync(c => c.ClientId == clientId);
        client.Level = level;
        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
