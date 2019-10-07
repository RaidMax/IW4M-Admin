using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Database;
using WebfrontCore.Middleware;

namespace WebfrontCore
{
    public class Startup
    {
        private readonly IHostingEnvironment _appHost;
        public static IConfigurationRoot Configuration { get; private set; }

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            _appHost = env;
        }

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
                        .AllowAnyHeader()
                        .AllowCredentials();
                    });
            });

            // Add framework services.
            var mvcBuilder = services.AddMvc()
                .ConfigureApplicationPartManager(_ =>
                {
                    foreach (var assembly in Program.Manager.GetPluginAssemblies())
                    {
                        if (assembly.FullName.Contains("Views"))
                        {
                            _.ApplicationParts.Add(new CompiledRazorAssemblyPart(assembly));
                        }

                        else if (assembly.FullName.Contains("Web"))
                        {
                            _.ApplicationParts.Add(new AssemblyPart(assembly));
                        }
                    }
                });

#if DEBUG
            mvcBuilder = mvcBuilder.AddRazorOptions(options => options.AllowRecompilingViewsOnFileChange = true);
            services.Configure<RazorViewEngineOptions>(_options =>
            {
                _options.ViewLocationFormats.Add(@"/Views/Plugins/{1}/{0}" + RazorViewEngine.ViewExtension);
            });
#endif

            foreach (var asm in Program.Manager.GetPluginAssemblies())
            {
                mvcBuilder.AddApplicationPart(asm);
            }

            services.AddEntityFrameworkSqlite()
                .AddDbContext<DatabaseContext>();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.AccessDeniedPath = "/";
                    options.LoginPath = "/";
                });

#if DEBUG
            services.AddLogging(_builder =>
            {
                _builder.AddDebug();
            });
#endif
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            if (Program.Manager.GetApplicationSettings().Configuration().EnableWebfrontConnectionWhitelist)
            {
                app.UseMiddleware<IPWhitelist>(Program.Manager.GetApplicationSettings().Configuration().WebfrontConnectionWhitelist);
            }

            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseCors("AllowAll");

            // prevents banned/demoted users from keeping their claims
            app.UseMiddleware<ClaimsPermissionRemoval>(Program.Manager);

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
