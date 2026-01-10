using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.RateLimiting;
using Data.Abstractions;
using Data.Helpers;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
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

public class Program
{
    public static IManager Manager = null!;
    private static WebApplication? _webApp;

    public static IServiceProvider InitializeServices(Action<IServiceCollection> registerDependenciesAction,
        string bindUrl)
    {
        _webApp = BuildWebApp(registerDependenciesAction, bindUrl);
        Manager = _webApp.Services.GetRequiredService<IManager>();
        return _webApp.Services;
    }

    public static Task GetWebHostTask(CancellationToken cancellationToken)
    {
        return _webApp?.RunAsync(cancellationToken) ?? Task.CompletedTask;
    }

    private static WebApplication BuildWebApp(Action<IServiceCollection> registerDependenciesAction, string bindUrl)
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
        
        // Build a temporary service provider to get the config for SSL setup
        using var tempProvider = builder.Services.BuildServiceProvider();
        var appConfig = tempProvider.GetRequiredService<ApplicationConfiguration>();
        var webfrontConfig = appConfig.Webfront;

        // Configure Kestrel based on SSL settings
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxConcurrentConnections =
                int.Parse(Environment.GetEnvironmentVariable("MaxConcurrentRequests") ?? "1");
            kestrel.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);

            // Parse the bind URL to get host and port
            var uri = new Uri(bindUrl.Replace("0.0.0.0", "localhost"));
            var port = uri.Port;

            if (webfrontConfig.UseSsl && !string.IsNullOrEmpty(webfrontConfig.SslCertificatePath))
            {
                // HTTPS mode - listen with SSL certificate
                kestrel.ListenAnyIP(port, listenOptions =>
                {
                    if (string.IsNullOrEmpty(webfrontConfig.SslCertificatePassword))
                    {
                        listenOptions.UseHttps(webfrontConfig.SslCertificatePath);
                    }
                    else
                    {
                        listenOptions.UseHttps(webfrontConfig.SslCertificatePath, webfrontConfig.SslCertificatePassword);
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

        services.AddStackPolicy(options =>
        {
            options.MaxConcurrentRequests =
                int.Parse(Environment.GetEnvironmentVariable("MaxConcurrentRequests") ?? "1");
            options.RequestQueueLimit = int.Parse(Environment.GetEnvironmentVariable("RequestQueueLimit") ?? "1");
        });

        services.AddRateLimiter(options => options.AddConcurrencyLimiter("concurrencyPolicy", opt =>
        {
            opt.PermitLimit = 2;
            opt.QueueLimit = 25;
            opt.QueueProcessingOrder = QueueProcessingOrder.NewestFirst;
        }));

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
    }

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

        app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
            .RequireRateLimiting("concurrencyPolicy");

        app.MapControllers()
            .RequireRateLimiting("concurrencyPolicy");

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
