using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Security.Tests.Infrastructure;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;
using Permission = Data.Models.Client.EFClient.Permission;

namespace Security.Tests;

/// <summary>
/// S3/S5: service-level redaction of player IP addresses in WebfrontDataService, which the anonymous
/// and low-privilege API routes rely on. S7: privileged actions that should leave an audit record.
/// </summary>
public class DisclosureAndAuditTests
{
    private static IHttpContextAccessor AccessorFor(Permission? level, int clientId = 0)
    {
        var context = new DefaultHttpContext();
        if (level is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Sid, clientId.ToString()),
                new Claim(ClaimTypes.Role, level.Value.ToString())
            ], "login"));
        }

        return new HttpContextAccessor { HttpContext = context };
    }

    private static WebfrontDataService NewService(ManagerHarness harness, TestDatabase database,
        IHttpContextAccessor accessor, IMetaServiceV2? meta = null,
        IResourceQueryHelper<ClientResourceRequest, ClientResourceResponse>? search = null)
    {
        var translations = new SharedLibraryCore.Localization.TranslationLookup { Set = new Dictionary<string, string>() };
        return new WebfrontDataService(harness.Manager, null!, null!, null!, database.Clients, meta!, null!, null!,
            search!, null!, null!, accessor, null!, null!, database.Factory, null!, null!, null!, translations,
            harness.Configuration, [], [], null!, null!);
    }

    /// <summary>GET api/client/search carries no [Authorize]; this is what keeps IPs away from guests.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(Permission.Trusted)]
    [InlineData(Permission.Administrator)]
    public async Task Client_search_never_returns_or_filters_by_ip_without_ClientIPAddress_Read(Permission? level)
    {
        await using var database = await TestDatabase.CreateAsync();
        var harness = new ManagerHarness(database.Clients);
        ClientResourceRequest? forwarded = null;
        var (search, searchControl) = Fake<IResourceQueryHelper<ClientResourceRequest, ClientResourceResponse>>.Create();
        searchControl.On("QueryResource", args =>
        {
            forwarded = (ClientResourceRequest)args[0]!;
            return Task.FromResult(new ResourceQueryHelperResult<ClientResourceResponse>
            {
                Results =
                [
                    new ClientResourceResponse
                    {
                        ClientId = 3, CurrentClientName = "a", MatchedClientName = "a", ClientLevel = "Owner",
                        ClientLevelValue = Permission.Owner, CurrentClientIp = 16909060, MatchedClientIp = 16909060
                    }
                ]
            });
        });

        var service = NewService(harness, database, AccessorFor(level), search: search);
        var result = await service.SearchClientsAsync(new ClientResourceRequest { ClientName = "a", ClientIp = "1.2.3.4" });

        Assert.Null(forwarded!.ClientIp);
        Assert.All(result.Results, r =>
        {
            Assert.Null(r.CurrentClientIp);
            Assert.Null(r.MatchedClientIp);
        });
    }

    /// <summary>
    /// GET api/client/{id}/meta?metaType=AliasUpdate. The default Administrator set has MetaAliasUpdate.Read
    /// but not ClientIPAddress.Read (only SeniorAdmin and above see IPs). The alias history entries carry the
    /// IP each alias was seen from; the Blazor page hides that field behind Permissions.ClientIPAddress.Read.
    /// </summary>
    [Fact]
    public async Task Alias_history_meta_does_not_return_ip_addresses_without_ClientIPAddress_Read()
    {
        await using var database = await TestDatabase.CreateAsync();
        var harness = new ManagerHarness(database.Clients);
        var (meta, metaControl) = Fake<IMetaServiceV2>.Create();
        metaControl.On("GetRuntimeMeta", _ => Task.FromResult<IEnumerable<UpdatedAliasResponse>>(
        [
            new UpdatedAliasResponse { Name = "player", IPAddress = "198.51.100.77", Type = MetaType.AliasUpdate, IsSensitive = true }
        ]));

        Assert.DoesNotContain("ClientIPAddress.Read", harness.Configuration.Webfront.PermissionSets[nameof(Permission.Administrator)]);
        Assert.DoesNotContain("*", harness.Configuration.Webfront.PermissionSets[nameof(Permission.Administrator)]);

        var service = NewService(harness, database, AccessorFor(Permission.Administrator, 9), meta: meta);
        var result = (await service.GetClientMetaAsync(new ClientMetaRequest
        {
            ClientId = 3, Count = 10, MetaType = MetaType.AliasUpdate
        })).OfType<UpdatedAliasResponse>().ToList();

        Assert.NotEmpty(result);
        Assert.True(result.All(alias => string.IsNullOrEmpty(alias.IPAddress)),
            $"an Administrator session without ClientIPAddress.Read received alias-history IP '{result[0].IPAddress}'");
    }

    /// <summary>
    /// POST /Account/DisableTwoFactorForClient: an Owner removes another staff member's second factor.
    /// </summary>
    [Fact]
    public async Task Disabling_another_clients_two_factor_leaves_an_audit_record()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = await database.AddClientAsync(Permission.Owner, "owner");
        var adminId = await database.AddClientAsync(Permission.Administrator, "admin", twoFactorSecret: "FIXTURESECRET");
        var harness = new ManagerHarness(database.Clients);
        harness.AddServer();

        var service = NewService(harness, database, AccessorFor(Permission.Owner, ownerId));
        await service.DisableTwoFactorAsync(adminId);

        await using var context = database.Factory.CreateContext();
        var secret = (await context.Clients.SingleAsync(c => c.ClientId == adminId)).TwoFactorSecret;
        Assert.Null(secret);

        var historyRows = await context.EFChangeHistory.CountAsync();
        Assert.True(historyRows > 0 || harness.GameEvents.Count > 0 || harness.CoreEvents.Count > 0,
            $"client {adminId}'s two-factor secret was removed by client {ownerId} with {historyRows} EFChangeHistory rows, " +
            $"{harness.GameEvents.Count} game events and {harness.CoreEvents.Count} management events raised");
    }

    /// <summary>A non-Owner may only disable their own second factor.</summary>
    [Fact]
    public async Task Disabling_another_clients_two_factor_requires_Owner()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seniorId = await database.AddClientAsync(Permission.SeniorAdmin, "senior");
        var adminId = await database.AddClientAsync(Permission.Administrator, "admin", twoFactorSecret: "FIXTURESECRET");
        var harness = new ManagerHarness(database.Clients);

        // the cookie claims Owner; the level that counts is the one in the database
        var service = NewService(harness, database, AccessorFor(Permission.Owner, seniorId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DisableTwoFactorAsync(adminId));

        await using var context = database.Factory.CreateContext();
        Assert.Equal("FIXTURESECRET", (await context.Clients.SingleAsync(c => c.ClientId == adminId)).TwoFactorSecret);
    }
}
