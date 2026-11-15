using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;

namespace Security.Tests.Infrastructure;

/// <summary>
/// Loopback-only (127.0.0.1, ephemeral port) host that wires the webfront's real cookie events and real
/// middleware around stub endpoints. Data-protection keys are ephemeral so nothing is written to the user profile.
/// </summary>
public sealed class LoopbackHost : IAsyncDisposable
{
    internal static readonly Type ClaimsPermissionRemoval = typeof(WebfrontCore.Program).Assembly
        .GetType("WebfrontCore.Core.Middleware.ClaimsPermissionRemoval", true)!;

    private readonly WebApplication _app;
    public Uri Address { get; }

    private LoopbackHost(WebApplication app, Uri address)
    {
        _app = app;
        Address = address;
    }

    public static WebApplicationBuilder NewBuilder()
    {
        var builder = WebApplication.CreateBuilder(["--urls", "http://127.0.0.1:0"]);
        builder.Logging.ClearProviders();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        return builder;
    }

    /// <summary>Cookie authentication configured with the same events WebfrontCore/Program.cs registers.</summary>
    public static void AddWebfrontCookieAuthentication(IServiceCollection services)
    {
        var validate = ClaimsPermissionRemoval.GetMethod("ValidateAsync", BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<Func<CookieValidatePrincipalContext, Task>>();
        var signedIn = ClaimsPermissionRemoval.GetMethod("OnSignedIn", BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<Func<CookieSignedInContext, Task>>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.AccessDeniedPath = "/";
                options.LoginPath = "/";
                options.Events.OnValidatePrincipal += validate;
                options.Events.OnSignedIn += signedIn;
            });
        services.AddAuthorization();
    }

    /// <summary>
    /// Same order as WebfrontCore/Program.cs ConfigureMiddleware: routing, authentication,
    /// ClaimsPermissionRemoval, authorization. Adds /signin (issues the cookie the way
    /// BaseController.SignInAsync does) and /whoami (reports what the pipeline authenticated).
    /// </summary>
    public static async Task<LoopbackHost> StartSessionHostAsync(IManager manager)
    {
        var builder = NewBuilder();
        AddWebfrontCookieAuthentication(builder.Services);
        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware(ClaimsPermissionRemoval, manager);
        app.UseAuthorization();

        app.MapGet("/signin/{clientId:int}/{role}", async (HttpContext context, int clientId, string role) =>
        {
            var identity = new ClaimsIdentity([
                new Claim(ClaimTypes.Sid, clientId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ], "login");
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity), new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTime.UtcNow.AddMonths(3),
                    IsPersistent = true,
                    IssuedUtc = DateTime.UtcNow
                });
        });
        app.MapGet("/whoami", (HttpContext context) => context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(ClaimTypes.Role)?.Value ?? "authenticated"
            : "anonymous");

        // an endpoint behind real authorization, like [Authorize(Roles = "Owner")] on api/configuration
        app.MapGet("/administrators-only", () => "ok")
            .RequireAuthorization(policy => policy.RequireRole("Administrator"));

        return await StartAsync(app);
    }

    public static async Task<HttpStatusCode> StatusAsync(HttpClient client, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    public static async Task<LoopbackHost> StartAsync(WebApplication app)
    {
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        return new LoopbackHost(app, new Uri(address));
    }

    /// <summary>A client that never stores cookies: each request carries only what the test puts on it.</summary>
    public HttpClient NewClient() => new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    {
        BaseAddress = Address,
        Timeout = TimeSpan.FromSeconds(20)
    };

    /// <summary>Signs in and returns the raw Cookie header value, as a browser would hold it.</summary>
    public async Task<string> SignInAsync(HttpClient client, int clientId, string role)
    {
        using var response = await client.GetAsync($"/signin/{clientId}/{role}");
        response.EnsureSuccessStatusCode();
        return string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));
    }

    public static async Task<string> WhoAmIAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request);
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadAsStringAsync()
            : $"HTTP {(int)response.StatusCode}";
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
