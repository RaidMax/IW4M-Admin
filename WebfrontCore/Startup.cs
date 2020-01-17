using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Database;
using SharedLibraryCore.Interfaces;
using System.Net;
using System.Threading.Tasks;
using WebfrontCore.Middleware;

namespace WebfrontCore
{
    public class Startup
    {
        public static IConfigurationRoot Configuration { get; private set; }

        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables();

            Configuration = builder.Build();
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
                        .AllowAnyHeader();
                    });
            });

            // Add framework services.
            var mvcBuilder = services.AddMvc(_options => _options.SuppressAsyncSuffixInActionNames = false)
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
            mvcBuilder = mvcBuilder.AddRazorRuntimeCompilation();
            services.Configure<RazorViewEngineOptions>(_options =>
            {
                _options.ViewLocationFormats.Add(@"/Views/Plugins/{1}/{0}" + RazorViewEngine.ViewExtension);
            });
#endif

            foreach (var asm in Program.Manager.GetPluginAssemblies())
            {
                mvcBuilder.AddApplicationPart(asm);
            }

            services.AddHttpContextAccessor();

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

            services.AddSingleton(Program.Manager);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory, IManager manager)
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
                app.UseMiddleware<IPWhitelist>(manager.GetLogger(0), manager.GetApplicationSettings().Configuration().WebfrontConnectionWhitelist);
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
