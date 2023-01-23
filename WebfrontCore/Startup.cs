using System;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using Stats.Dtos;
using Stats.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Helpers;
using Stats.Client.Abstractions;
using Stats.Config;
using WebfrontCore.Controllers.API.Validation;
using WebfrontCore.Middleware;
using WebfrontCore.QueryHelpers;
using WebfrontCore.QueryHelpers.Models;

namespace WebfrontCore
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // allow CORS
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

            IEnumerable<Assembly> pluginAssemblies()
            {
                string pluginDir = $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}";

                if (Directory.Exists(pluginDir))
                {
                    var dllFileNames = Directory.GetFiles($"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}", "*.dll");
                    return dllFileNames.Select(_file => Assembly.LoadFrom(_file));
                }

                return Enumerable.Empty<Assembly>();
            }

            // Add framework services.
            var mvcBuilder = services.AddMvc(_options => _options.SuppressAsyncSuffixInActionNames = false)
                .AddFluentValidation()
                .ConfigureApplicationPartManager(_partManager =>
                {
                    foreach (var assembly in pluginAssemblies())
                    {
                        if (assembly.FullName.Contains("Views"))
                        {
                            _partManager.ApplicationParts.Add(new CompiledRazorAssemblyPart(assembly));
                        }

                        else if (assembly.FullName.Contains("Web"))
                        {
                            _partManager.ApplicationParts.Add(new AssemblyPart(assembly));
                        }
                    }
                });

#if DEBUG
            {
                mvcBuilder = mvcBuilder.AddRazorRuntimeCompilation();
                services.Configure<RazorViewEngineOptions>(_options =>
                {
                    _options.ViewLocationFormats.Add(@"/Views/Plugins/{1}/{0}" + RazorViewEngine.ViewExtension);
                    _options.ViewLocationFormats.Add("/Views/Plugins/Stats/Advanced.cshtml");
                });
            }
#endif

            foreach (var asm in pluginAssemblies())
            {
                mvcBuilder.AddApplicationPart(asm);
            }

            services.AddHttpContextAccessor();
            
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.AccessDeniedPath = "/";
                    options.LoginPath = "/";
                    options.Events.OnValidatePrincipal += ClaimsPermissionRemoval.ValidateAsync;
                    options.Events.OnSignedIn += ClaimsPermissionRemoval.OnSignedIn;
                });

            services.AddSingleton(Program.Manager);
            services.AddSingleton<IResourceQueryHelper<ChatSearchQuery, MessageResponse>, ChatResourceQueryHelper>();
            services.AddTransient<IValidator<FindClientRequest>, FindClientRequestValidator>();
            services.AddSingleton<IResourceQueryHelper<FindClientRequest, FindClientResult>, ClientService>();
            services.AddSingleton<IResourceQueryHelper<StatsInfoRequest, StatsInfoResult>, StatsResourceQueryHelper>();
            services.AddSingleton<IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo>, AdvancedClientStatsResourceQueryHelper>();
            services.AddScoped(sp =>
                Program.ApplicationServiceProvider
                    .GetRequiredService<IResourceQueryHelper<ClientResourceRequest, ClientResourceResponse>>());
            services.AddSingleton(typeof(IDataValueCache<,>), typeof(DataValueCache<,>));
            // todo: this needs to be handled more gracefully
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<DefaultSettings>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<ILoggerFactory>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IConfigurationHandlerFactory>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IDatabaseContextFactory>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IAuditInformationRepository>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<ITranslationLookup>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IEnumerable<IManagerCommand>>());
#pragma warning disable CS0618
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IMetaService>());
#pragma warning restore CS0618
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IMetaServiceV2>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<ApplicationConfiguration>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<ClientService>());
            services.AddSingleton<IResourceQueryHelper<BanInfoRequest, BanInfo>, BanInfoResourceQueryHelper>(); 
            services.AddSingleton(
                Program.ApplicationServiceProvider.GetRequiredService<IServerDistributionCalculator>());
            services.AddSingleton(Program.ApplicationServiceProvider
                .GetRequiredService<IConfigurationHandler<DefaultSettings>>());
            services.AddSingleton(Program.ApplicationServiceProvider
                .GetRequiredService<IGeoLocationService>());
            services.AddSingleton(Program.ApplicationServiceProvider
                            .GetRequiredService<StatsConfiguration>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IServerDataViewer>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IInteractionRegistration>());
            services.AddSingleton(Program.ApplicationServiceProvider.GetRequiredService<IRemoteCommandService>());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
        {
            app.UseStatusCodePages(_context =>
            {
                if (_context.HttpContext.Response.StatusCode == (int)HttpStatusCode.NotFound)
                {
                    _context.HttpContext.Response.Redirect($"/Home/ResponseStatusCode?statusCode={_context.HttpContext.Response.StatusCode}");
                }

                return Task.CompletedTask;
            });

            if (env.EnvironmentName == "Development")
            {
                app.UseDeveloperExceptionPage();
            }

            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            if (Program.Manager.GetApplicationSettings().Configuration().EnableWebfrontConnectionWhitelist)
            {
                app.UseMiddleware<IPWhitelist>(serviceProvider.GetService<ILogger<IPWhitelist>>(), serviceProvider.GetRequiredService<ApplicationConfiguration>().WebfrontConnectionWhitelist);
            }

            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseCors("AllowAll");

            // prevents banned/demoted users from keeping their claims
            app.UseMiddleware<ClaimsPermissionRemoval>(Program.Manager);

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
