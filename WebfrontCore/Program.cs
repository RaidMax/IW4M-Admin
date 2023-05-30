using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore
{
    public class Program
    {
        public static IManager Manager;
        private static IWebHost _webHost;

        public static IServiceProvider InitializeServices(Action<IServiceCollection> registerDependenciesAction, string bindUrl)
        {
            _webHost = BuildWebHost(registerDependenciesAction, bindUrl);
            Manager = _webHost.Services.GetRequiredService<IManager>();
            return _webHost.Services;
        }

        public static Task GetWebHostTask(CancellationToken cancellationToken)
        {
            return _webHost?.RunAsync(cancellationToken);
        }
        
        private static IWebHost BuildWebHost(Action<IServiceCollection> registerDependenciesAction, string bindUrl)
        {
            return new WebHostBuilder()
#if DEBUG
                .UseContentRoot(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\", "WebfrontCore")))
#else
                .UseContentRoot(SharedLibraryCore.Utilities.OperatingDirectory)
#endif
                .UseUrls(bindUrl)
                .UseKestrel(cfg =>
                {
                    cfg.Limits.MaxConcurrentConnections =
                        int.Parse(Environment.GetEnvironmentVariable("MaxConcurrentRequests") ?? "1");
                    cfg.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
                })
                .ConfigureServices(registerDependenciesAction)
                .UseStartup<Startup>()
                .Build();
        }
    }
}
