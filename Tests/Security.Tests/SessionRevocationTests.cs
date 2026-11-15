using System.Collections;
using System.Reflection;
using Security.Tests.Infrastructure;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using EFClient = SharedLibraryCore.Database.Models.EFClient;
using Permission = Data.Models.Client.EFClient.Permission;

namespace Security.Tests;

/// <summary>
/// S6: a three-month persistent cookie issued before a demotion, password change or restart.
/// Runs the real cookie events and the real ClaimsPermissionRemoval middleware
/// (WebfrontCore/Core/Middleware/ClaimsPermissionRemoval.cs) on a loopback-only listener.
/// The "browser" here deliberately ignores Set-Cookie, as someone keeping access would.
/// </summary>
public class SessionRevocationTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private ManagerHarness _harness = null!;
    private LoopbackHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        ResetPrivilegedSet();
        _database = await TestDatabase.CreateAsync();
        // a second privileged client keeps the middleware's in-memory set populated throughout
        await _database.AddClientAsync(Permission.Owner, "owner");
        _harness = new ManagerHarness(_database.Clients);
        _host = await LoopbackHost.StartSessionHostAsync(_harness.Manager);
        _client = _host.NewClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.DisposeAsync();
        await _database.DisposeAsync();
        ResetPrivilegedSet();
    }

    /// <summary>The set is static, i.e. per process. Clearing it is exactly what a restart does.</summary>
    private static void ResetPrivilegedSet() =>
        ((IDictionary)LoopbackHost.ClaimsPermissionRemoval
            .GetField("PrivilegedClientIds", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!).Clear();

    private async Task DemoteAsync(int clientId, Permission newLevel)
    {
        await _database.SetLevelAsync(clientId, newLevel);
        _harness.RaiseExecuted(new GameEvent
        {
            Type = GameEvent.EventType.ChangePermission,
            Extra = newLevel,
            Origin = new EFClient { ClientId = 1 },
            Target = new EFClient { ClientId = clientId }
        });
    }

    private async Task<(int ClientId, string Cookie)> SignedInAsync(Permission level)
    {
        var clientId = await _database.AddClientAsync(level, level.ToString().ToLowerInvariant());
        var cookie = await _host.SignInAsync(_client, clientId, level.ToString());
        Assert.Equal(level.ToString(), await LoopbackHost.WhoAmIAsync(_client, cookie));
        return (clientId, cookie);
    }

    [Fact]
    public async Task Cookie_issued_before_a_move_between_privileged_levels_is_rejected()
    {
        var (clientId, cookie) = await SignedInAsync(Permission.Administrator);

        await DemoteAsync(clientId, Permission.Moderator);

        Assert.Equal("anonymous", await LoopbackHost.WhoAmIAsync(_client, cookie));
    }

    [Fact]
    public async Task Cookie_issued_before_a_password_change_is_rejected()
    {
        var (clientId, cookie) = await SignedInAsync(Permission.Administrator);

        _harness.RaiseExecuted(new GameEvent
        {
            Type = GameEvent.EventType.Command,
            Extra = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SetPasswordCommand)),
            Origin = new EFClient { ClientId = clientId }
        });

        Assert.Equal("anonymous", await LoopbackHost.WhoAmIAsync(_client, cookie));
    }

    [Theory]
    [InlineData(Permission.User)]
    [InlineData(Permission.Flagged)]
    [InlineData(Permission.Banned)]
    public async Task Cookie_issued_before_a_demotion_below_Trusted_is_rejected(Permission demotedTo)
    {
        var (clientId, cookie) = await SignedInAsync(Permission.Administrator);

        await DemoteAsync(clientId, demotedTo);

        var first = await LoopbackHost.WhoAmIAsync(_client, cookie);
        var second = await LoopbackHost.WhoAmIAsync(_client, cookie);
        Assert.True(first == "anonymous" && second == "anonymous",
            $"after demotion to {demotedTo}, the pre-demotion cookie was still authenticated as '{first}' then '{second}'");
    }

    [Fact]
    public async Task Role_gated_endpoint_refuses_a_cookie_issued_before_a_demotion()
    {
        var (clientId, cookie) = await SignedInAsync(Permission.Administrator);
        Assert.Equal(System.Net.HttpStatusCode.OK, await LoopbackHost.StatusAsync(_client, "/administrators-only", cookie));

        await DemoteAsync(clientId, Permission.User);

        var status = await LoopbackHost.StatusAsync(_client, "/administrators-only", cookie);
        Assert.True(status != System.Net.HttpStatusCode.OK,
            $"after demotion to User, an endpoint requiring role Administrator answered {(int)status} to the pre-demotion cookie");
    }

    [Fact]
    public async Task Cookie_invalidated_by_a_move_between_privileged_levels_stays_invalid_after_a_restart()
    {
        var (clientId, cookie) = await SignedInAsync(Permission.Administrator);
        await DemoteAsync(clientId, Permission.Moderator);
        Assert.Equal("anonymous", await LoopbackHost.WhoAmIAsync(_client, cookie));

        ResetPrivilegedSet(); // process restart: the taint was only in memory

        var role = await LoopbackHost.WhoAmIAsync(_client, cookie);
        Assert.True(role == "anonymous",
            $"after a restart, the cookie issued before a move from Administrator to Moderator was accepted again as '{role}'");
    }

    [Fact]
    public async Task Cookie_invalidated_by_a_password_change_stays_invalid_after_a_restart()
    {
        var (clientId, cookie) = await SignedInAsync(Permission.Administrator);
        _harness.RaiseExecuted(new GameEvent
        {
            Type = GameEvent.EventType.Command,
            Extra = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SetPasswordCommand)),
            Origin = new EFClient { ClientId = clientId }
        });
        Assert.Equal("anonymous", await LoopbackHost.WhoAmIAsync(_client, cookie));

        ResetPrivilegedSet(); // process restart: the taint was only in memory

        var role = await LoopbackHost.WhoAmIAsync(_client, cookie);
        Assert.True(role == "anonymous",
            $"after a restart, the cookie issued before a password change was accepted again as '{role}'");
    }

    [Fact]
    public async Task Cookie_issued_before_a_demotion_to_Trusted_does_not_keep_the_old_role()
    {
        var (clientId, cookie) = await SignedInAsync(Permission.Administrator);

        await DemoteAsync(clientId, Permission.Trusted);

        var role = await LoopbackHost.WhoAmIAsync(_client, cookie);
        Assert.True(role != nameof(Permission.Administrator),
            $"after demotion to Trusted, the pre-demotion cookie still carried role '{role}'");
    }

    [Fact]
    public async Task Cookie_of_a_client_demoted_before_a_restart_is_rejected_after_it()
    {
        var (clientId, cookie) = await SignedInAsync(Permission.Administrator);

        // demoted in the database, then the process restarts: the in-memory set is rebuilt from the database
        await _database.SetLevelAsync(clientId, Permission.User);
        ResetPrivilegedSet();

        var first = await LoopbackHost.WhoAmIAsync(_client, cookie);
        var second = await LoopbackHost.WhoAmIAsync(_client, cookie);
        Assert.True(first == "anonymous" && second == "anonymous",
            $"after a restart, the cookie of a client who is now User was still authenticated as '{first}' then '{second}'");
    }
}
