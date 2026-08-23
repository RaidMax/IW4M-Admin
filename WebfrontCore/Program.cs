using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Data.Abstractions;
using Data.Helpers;
using Data.Models.Client;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using Stats.Dtos;
using Stats.Helpers;
using WebfrontCore.Controllers.API.Validation;
using Microsoft.AspNetCore.Components.Authorization;
using WebfrontCore.Components;
using WebfrontCore.Core.Auth;
using WebfrontCore.Core.Middleware;
using WebfrontCore.Core.QueryHelpers;
using WebfrontCore.Core.QueryHelpers.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore;

internal sealed class NoopHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class Program
{
    public static IManager Manager = null!;
    private static WebApplication? _webApp;

    public static IServiceProvider InitializeServices(Action<IServiceCollection> registerDependenciesAction,
        ApplicationConfiguration appConfig)
    {
        _webApp = BuildWebApp(registerDependenciesAction, appConfig);
        Manager = _webApp.Services.GetRequiredService<IManager>();
        return _webApp.Services;
    }

    public static Task GetWebHostTask(CancellationToken cancellationToken)
    {
        return _webApp?.RunAsync(cancellationToken) ?? Task.CompletedTask;
    }

    private static WebApplication BuildWebApp(Action<IServiceCollection> registerDependenciesAction, ApplicationConfiguration appConfig)
    {
#if DEBUG
        var contentRoot =
            Path.GetFullPath(Path.Combine(Utilities.OperatingDirectory, @"..\..\..\", "WebfrontCore"));
#else
        var contentRoot = Utilities.OperatingDirectory;
#endif
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot")
        });

        // Register dependencies first so we can access ApplicationConfiguration
        registerDependenciesAction(builder.Services);

        // Suppress the default ConsoleLifetime. It registers its own CancelKeyPress
        // and ProcessExit handlers which race with the ones in Application.Main,
        // only cancelling the host's internal token and leaving the ApplicationManager
        // token live. Under Docker SIGTERM that produced partial shutdowns that
        // exceeded the 10s grace and triggered SIGKILL. Shutdown is driven from
        // Application.Main via ApplicationManager.Stop().
        builder.Services.Replace(ServiceDescriptor.Singleton<IHostLifetime, NoopHostLifetime>());
        
        // before the migration has run we still need to respect the old bind url
#pragma warning disable CS0618 // Type or member is obsolete
        var bindUrl = appConfig.WebfrontBindUrl ?? appConfig.Webfront.BindUrl;
#pragma warning restore CS0618 // Type or member is obsolete

        // Configure Kestrel based on SSL settings
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxConcurrentConnections =
                int.Parse(Environment.GetEnvironmentVariable("MaxConcurrentRequests") ?? "1");
            kestrel.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);

            // Parse the bind URL to get host and port
            var uri = new Uri(bindUrl.Replace("0.0.0.0", "localhost"));
            var port = uri.Port;

            if (appConfig.Webfront.UseSsl && !string.IsNullOrEmpty(appConfig.Webfront.SslCertificatePath))
            {
                // HTTPS mode - listen with SSL certificate
                kestrel.ListenAnyIP(port, listenOptions =>
                {
                    if (string.IsNullOrEmpty(appConfig.Webfront.SslCertificatePassword))
                    {
                        listenOptions.UseHttps(appConfig.Webfront.SslCertificatePath);
                    }
                    else
                    {
                        listenOptions.UseHttps(appConfig.Webfront.SslCertificatePath,
                            appConfig.Webfront.SslCertificatePassword);
                    }
                });
            }
            else
            {
                // HTTP mode - standard listen
                kestrel.ListenAnyIP(port);
            }
        });

        // This is needed because the Application project doesn't use Microsoft.NET.Sdk.Web
        builder.WebHost.UseStaticWebAssets();

        builder.Services.AddServerSideBlazor(options =>
        {
            // Speed up detection of a dropped mobile connection
            options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
            options.DetailedErrors = Utilities.IsDevelopment;
        });

        builder.Services.AddSignalR(options =>
        {
            options.KeepAliveInterval = TimeSpan.FromSeconds(5); // Default is 15
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(10); // Default is 30
            options.EnableDetailedErrors = Utilities.IsDevelopment;
            options.MaximumReceiveMessageSize = 512 * 1024; // 256KB (default is 32KB) - needed for chart history data
        });

        ConfigureServices(builder.Services);

        var app = builder.Build();

        ConfigureMiddleware(app);

        return app;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                policyBuilder =>
                {
                    policyBuilder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddConcurrencyLimiter("concurrencyPolicy", opt =>
            {
                opt.PermitLimit = 30;
                opt.QueueLimit = 10;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            options.AddPolicy("liveRadar", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        // Add framework services
        var mvcBuilder = services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false);
        services.AddFluentValidationAutoValidation().AddFluentValidationClientsideAdapters();

        // Add WebfrontCore assembly for controller discovery (CreateSlimBuilder doesn't auto-discover)
        mvcBuilder.AddApplicationPart(typeof(Program).Assembly);

        foreach (var asm in GetPluginAssemblies())
        {
            mvcBuilder.AddApplicationPart(asm);
        }

        mvcBuilder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        services.AddHttpContextAccessor();
        services.AddHttpClient();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.AccessDeniedPath = "/";
                options.LoginPath = "/";
                options.Events.OnValidatePrincipal += ClaimsPermissionRemoval.ValidateAsync;
                options.Events.OnSignedIn += ClaimsPermissionRemoval.OnSignedIn;
            });

        services.AddTransient<IValidator<FindClientRequest>, FindClientRequestValidator>();
        services.AddSingleton<IResourceQueryHelper<FindClientRequest, FindClientResult>, ClientService>();
        services.AddSingleton<IResourceQueryHelper<StatsInfoRequest, StatsInfoResult>, StatsResourceQueryHelper>();
        services
            .AddSingleton<IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo>,
                AdvancedClientStatsResourceQueryHelper>();
        services.AddSingleton(typeof(IDataValueCache<,>), typeof(DataValueCache<,>));
        services.AddSingleton<IResourceQueryHelper<BanInfoRequest, BanInfo>, BanInfoResourceQueryHelper>();

        services.AddRazorComponents()
            .AddInteractiveServerComponents(options => { options.DetailedErrors = Utilities.IsDevelopment; });

        services.AddScoped<AppState>();
        services.AddScoped<IToastService, ToastService>();
        services.AddTransient<CookieForwardingHandler>();

        services.AddScoped<IWebfrontDataService, WebfrontDataService>();

        services
            .AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();
        services
            .AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();
        services.AddCascadingAuthenticationState();

        services.AddScoped<IActionService, ActionService>();
        services.AddScoped<ITwoFactorAuthService, TwoFactorAuthService>();

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<Core.OpenApi.TagDescriptionsTransformer>();
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(ApiDocsPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                {
                    var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
                    return Enum.TryParse<EFClient.Permission>(role, out var level)
                           && level >= EFClient.Permission.SeniorAdmin;
                }));
    }

    private const string ApiDocsPolicy = "ApiDocs.View";

    private static void ConfigureMiddleware(WebApplication app)
    {
        var appConfig = app.Services.GetRequiredService<ApplicationConfiguration>();
        var manager = app.Services.GetRequiredService<IManager>();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        if (appConfig.Webfront.EnableConnectionWhitelist)
        {
            app.UseMiddleware<IPWhitelist>(
                app.Services.GetService<ILogger<IPWhitelist>>(),
                appConfig.Webfront.ConnectionWhitelist);
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseCors("AllowAll");
        app.UseMiddleware<ClaimsPermissionRemoval>(manager);
        app.UseAuthorization();
        app.UseStatusCodePagesWithReExecute("/NotFound", createScopeForStatusCodePages: true);
        app.UseAntiforgery();
        app.UseRateLimiter();
        // serve user provided files
        app.UseStaticFiles();

        app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
            .RequireRateLimiting("concurrencyPolicy");

        app.MapControllers()
            .RequireRateLimiting("concurrencyPolicy");

        var openApi = app.MapOpenApi("/openapi/{documentName}.json");
        var scalar = app.MapScalarApiReference("/api-docs", options =>
        {
            options.WithTitle("IW4MAdmin API")
                .WithOpenApiRoutePattern("/openapi/{documentName}.json");
        });

        if (!app.Environment.IsDevelopment())
        {
            openApi.RequireAuthorization(ApiDocsPolicy);
            scalar.RequireAuthorization(ApiDocsPolicy);
        }

        app.MapStaticAssets();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(GetPluginAssemblies().ToArray());
    }

    private static IEnumerable<Assembly> GetPluginAssemblies()
    {
        var pluginDir = $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}";

        if (!Directory.Exists(pluginDir))
            return [];
        var dllFileNames =
            Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}",
                "*.dll");
        return dllFileNames.Select(Assembly.LoadFrom);
    }
}
