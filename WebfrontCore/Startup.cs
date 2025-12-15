using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using Stats.Dtos;
using Stats.Helpers;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.RateLimiting;
using Data.Abstractions;
using Data.Helpers;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.RateLimiting;
using SharedLibraryCore;
using WebfrontCore.Components;
using WebfrontCore.Controllers.API.Validation;
using WebfrontCore.Middleware;
using WebfrontCore.QueryHelpers;
using WebfrontCore.QueryHelpers.Models;

namespace WebfrontCore;

public class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        // 1. CORS Configuration
        services.AddCors(_options =>
        {
            _options.AddPolicy("AllowAll",
                _builder =>
                {
                    _builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
        });

        // 2. Custom Stack Policies
        services.AddStackPolicy(options =>
        {
            options.MaxConcurrentRequests =
                int.Parse(Environment.GetEnvironmentVariable("MaxConcurrentRequests") ?? "1");
            options.RequestQueueLimit = int.Parse(Environment.GetEnvironmentVariable("RequestQueueLimit") ?? "1");
        });

        // 3. Rate Limiter
        services.AddRateLimiter(options => options.AddConcurrencyLimiter("concurrencyPolicy", opt =>
        {
            opt.PermitLimit = 2;
            opt.QueueLimit = 25;
            opt.QueueProcessingOrder = QueueProcessingOrder.NewestFirst;
        }));

        // Add framework services.
        var mvcBuilder = services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false);
        services.AddFluentValidationAutoValidation().AddFluentValidationClientsideAdapters();

        foreach (var asm in PluginAssemblies())
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

        // 5. Domain Services / Singletons
        services.AddSingleton<IResourceQueryHelper<ChatSearchQuery, MessageResponse>, ChatResourceQueryHelper>();
        // Note: Kept Validator registration for DI, but removed MVC auto-validation adapters
        services.AddTransient<IValidator<FindClientRequest>, FindClientRequestValidator>();
        services.AddSingleton<IResourceQueryHelper<FindClientRequest, FindClientResult>, ClientService>();
        services.AddSingleton<IResourceQueryHelper<StatsInfoRequest, StatsInfoResult>, StatsResourceQueryHelper>();
        services
            .AddSingleton<IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo>,
                AdvancedClientStatsResourceQueryHelper>();

        services.AddSingleton(typeof(IDataValueCache<,>), typeof(DataValueCache<,>));
        services.AddSingleton<IResourceQueryHelper<BanInfoRequest, BanInfo>, BanInfoResourceQueryHelper>();

        services.AddRazorComponents()
            .AddInteractiveServerComponents(options => { options.DetailedErrors = true; });

        services.AddScoped<Services.AppState>();
        services.AddScoped<Services.IZeroJsInterop, Services.ZeroJsInterop>();
        services.AddScoped<Services.IToastService, Services.ToastService>();
        services.AddTransient<Services.CookieForwardingHandler>();

        services.AddHttpClient<Services.IWebfrontApiClient, Services.WebfrontApiClient>((sp, client) =>
        {
            var manager = sp.GetService<IManager>();
            var webfrontUrl = manager?.GetApplicationSettings()?.Configuration()?.WebfrontUrl ?? "http://127.0.0.1:1624";
            client.BaseAddress = new Uri(webfrontUrl);
        }).AddHttpMessageHandler<Services.CookieForwardingHandler>();

        services.AddScoped<Services.IActionService, Services.ActionService>();
        return;

        IEnumerable<Assembly> PluginAssemblies()
        {
            var pluginDir = $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}";

            if (!Directory.Exists(pluginDir)) return [];
            var dllFileNames =
                Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}",
                    "*.dll");
            return dllFileNames.Select(Assembly.LoadFrom);
        }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        // Status code handling is done via UseStatusCodePagesWithReExecute below

        if (env.EnvironmentName == "Development")
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        if (Program.Manager.GetApplicationSettings().Configuration().EnableWebfrontConnectionWhitelist)
        {
            app.UseMiddleware<IPWhitelist>(serviceProvider.GetService<ILogger<IPWhitelist>>(),
                serviceProvider.GetRequiredService<ApplicationConfiguration>().WebfrontConnectionWhitelist);
        }

        // Static files from wwwroot
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAntiforgery();

        app.UseAuthentication();
        app.UseCors("AllowAll");

        app.UseMiddleware<ClaimsPermissionRemoval>(Program.Manager);

        app.UseAuthorization();
        app.UseStatusCodePagesWithReExecute("/NotFound", createScopeForStatusCodePages: true);
        app.UseRateLimiter();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}")
                .RequireRateLimiting("concurrencyPolicy");
            
            // Pure Blazor Routing with static assets chained (serves _framework/blazor.web.js)
            endpoints.MapStaticAssets();
            endpoints.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();
        });
    }
}
