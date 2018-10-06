using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore
{
    public class Program
    {
        public static IManager Manager;

        static void Main(string[] args)
        {
            throw new System.Exception("Webfront core cannot be run as a standalone application");
        }

        public static void Init(IManager mgr)
        {
            Manager = mgr;
            BuildWebHost().Run();
        }

        public static IWebHost BuildWebHost()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            return new WebHostBuilder()
#if DEBUG
                .UseContentRoot(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\", "WebfrontCore")))
#else
                .UseContentRoot(SharedLibraryCore.Utilities.OperatingDirectory)
#endif
                .UseUrls(Manager.GetApplicationSettings().Configuration().WebfrontBindUrl)
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();
        }
    }
}
