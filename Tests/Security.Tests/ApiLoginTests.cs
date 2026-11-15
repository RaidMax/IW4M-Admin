using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Security.Tests.Infrastructure;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API;
using WebfrontCore.Controllers.API.Models;
using WebfrontCore.Core.Auth;
using WebfrontCore.Core.Services;
using Permission = Data.Models.Client.EFClient.Permission;

namespace Security.Tests;

/// <summary>
/// S3/S5: the JSON login path, POST api/client/{clientId}/login
/// (ClientController.Login -> WebfrontDataService.LoginAsync), compared with POST /Account/Login.
/// </summary>
public class ApiLoginTests
{
    private const string Password = "fixture-password-1";

    private static async Task<(WebfrontDataService Service, ManagerHarness Harness, int ClientId)> ArrangeAsync(
        TestDatabase database, Permission level, bool requireTwoFactor)
    {
        var clientId = await database.AddClientAsync(level, "fixture", Password);
        var tokens = (ITokenAuthentication)Activator.CreateInstance(
            typeof(IW4MAdmin.Application.Program).Assembly.GetType("IW4MAdmin.Application.Misc.TokenAuthentication", true)!,
            nonPublic: true)!;
        var harness = new ManagerHarness(database.Clients, tokens);
        harness.AddServer();
        harness.Configuration.Webfront.RequireTwoFactorForPrivilegedClients = requireTwoFactor;
        var service = PenaltyHierarchyTests.NewDataService(harness, database, new HttpContextAccessor());
        return (service, harness, clientId);
    }

    private static ServiceLoginRequest Request(int clientId, string password) => new()
    {
        ClientId = clientId, Password = password, IpAddress = "192.0.2.1"
    };

    [Fact]
    public async Task Api_login_rejects_a_wrong_password()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (service, _, clientId) = await ArrangeAsync(database, Permission.Moderator, false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(Request(clientId, "wrong")));
    }

    [Fact]
    public async Task Api_login_rejects_a_client_that_has_no_password_set()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (service, _, _) = await ArrangeAsync(database, Permission.Moderator, false);
        var passwordless = await database.AddClientAsync(Permission.Owner, "passwordless");

        foreach (var attempt in new[] { "", "null", Password })
        {
            await Assert.ThrowsAnyAsync<Exception>(() => service.LoginAsync(Request(passwordless, attempt)));
        }
    }

    /// <summary>
    /// With Webfront.RequireTwoFactorForPrivilegedClients on, /Account/Login marks a Moderator+ without 2FA
    /// with the PendingTwoFactorEnrollment claim, which PermissionAuthorizationHandler uses to refuse
    /// everything except the profile page. The API login must produce a session under the same constraint.
    /// </summary>
    [Fact]
    public async Task Api_login_session_is_constrained_by_pending_two_factor_enrollment()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (service, harness, clientId) = await ArrangeAsync(database, Permission.Administrator, true);

        var principal = await service.LoginAsync(Request(clientId, Password));

        Assert.True(principal.HasClaim(c => c.Type == WebfrontClaimTypes.PendingTwoFactorEnrollment),
            "API login of an Administrator without 2FA, with RequireTwoFactorForPrivilegedClients=true, " +
            "issued a principal without the PendingTwoFactorEnrollment claim: " +
            string.Join(", ", principal.Claims.Select(c => c.Type.Split('/').Last())));

        // consequence: the real authorization handler would refuse this policy for a constrained session
        var handler = new PermissionAuthorizationHandler(harness.Configuration);
        var requirement = new PermissionRequirement(WebfrontEntity.AuditPage, WebfrontPermission.Read);
        var context = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext([requirement], principal, null);
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// The handler itself: a session that does carry the claim is refused everything but the profile page.
    /// </summary>
    [Fact]
    public async Task Pending_enrollment_claim_blocks_every_policy_except_the_profile_page()
    {
        var configuration = new ApplicationConfiguration();
        var handler = new PermissionAuthorizationHandler(configuration);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, nameof(Permission.Owner)),
            new Claim(WebfrontClaimTypes.PendingTwoFactorEnrollment, "true")
        ], "login"));

        foreach (var entity in Enum.GetValues<WebfrontEntity>())
        {
            var requirement = new PermissionRequirement(entity, WebfrontPermission.Read);
            var context = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext([requirement], principal, null);
            await handler.HandleAsync(context);
            Assert.Equal(entity == WebfrontEntity.ProfilePage, context.HasSucceeded);
        }
    }

    /// <summary>
    /// S3/S7: the IP address written to the login audit record. Webfront.TrustedProxyAddresses is empty and the
    /// peer is not loopback, so nothing the peer puts in X-Forwarded-For may reach the record.
    /// Real ClientController, real forwarded-headers options, stubbed data service.
    /// </summary>
    [Fact]
    public async Task Login_audit_address_ignores_forwarded_header_from_an_untrusted_peer()
    {
        var harness = new ManagerHarness();
        harness.AddServer();
        ServiceLoginRequest? seen = null;
        var (dataService, dataControl) = Fake<IWebfrontDataService>.Create();
        dataControl.On(nameof(IWebfrontDataService.LoginAsync), args =>
        {
            seen = (ServiceLoginRequest)args[0]!;
            return Task.FromResult(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Sid, "5"), new Claim(ClaimTypes.Role, nameof(Permission.Trusted))
            ], "login")));
        });

        var builder = LoopbackHost.NewBuilder();
        LoopbackHost.AddWebfrontCookieAuthentication(builder.Services);
        builder.Services.AddSingleton(harness.Manager);
        builder.Services.AddSingleton(dataService);
        builder.Services.AddControllers().ConfigureApplicationPartManager(parts =>
        {
            parts.ApplicationParts.Clear();
            parts.ApplicationParts.Add(new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(ClientController).Assembly));
        });

        var app = builder.Build();
        var peer = IPAddress.Parse("203.0.113.9");
        // the listener is loopback-only; present the request to the pipeline as coming from a remote peer
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = peer;
            return next();
        });
        var forwardedOptions = (ForwardedHeadersOptions)typeof(WebfrontCore.Program)
            .GetMethod("CreateForwardedHeadersOptions", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new WebfrontConfiguration()])!;
        app.UseForwardedHeaders(forwardedOptions);
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await using var host = await LoopbackHost.StartAsync(app);
        using var client = host.NewClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/client/5/login");
        message.Headers.Add("X-Forwarded-For", "10.20.30.40");
        message.Content = JsonContent.Create(new { password = "anything" });
        using var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(seen);
        Assert.True(seen.IpAddress == peer.ToString(),
            $"login from peer {peer} with a self-supplied X-Forwarded-For was recorded as coming from '{seen.IpAddress}'");
    }
}
