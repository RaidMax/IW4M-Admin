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
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\", "WebfrontCore"));
#else
        var contentRoot = Utilities.OperatingDirectory;
#endif
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot")
        });

        builder.WebHost.UseUrls(bindUrl);

        // This is needed because the Application project doesn't use Microsoft.NET.Sdk.Web
        builder.WebHost.UseStaticWebAssets();

        builder.WebHost.ConfigureKestrel(cfg =>
        {
            cfg.Limits.MaxConcurrentConnections =
                int.Parse(Environment.GetEnvironmentVariable("MaxConcurrentRequests") ?? "1");
            cfg.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
        });

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
        });

        registerDependenciesAction(builder.Services);
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

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.AccessDeniedPath = "/";
                options.LoginPath = "/";
                options.Events.OnValidatePrincipal += ClaimsPermissionRemoval.ValidateAsync;
                options.Events.OnSignedIn += ClaimsPermissionRemoval.OnSignedIn;
            });

        services.AddSingleton<IResourceQueryHelper<ChatSearchQuery, MessageResponse>, ChatResourceQueryHelper>();
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

        if (appConfig.EnableWebfrontConnectionWhitelist)
        {
            app.UseMiddleware<IPWhitelist>(
                app.Services.GetService<ILogger<IPWhitelist>>(),
                appConfig.WebfrontConnectionWhitelist);
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
