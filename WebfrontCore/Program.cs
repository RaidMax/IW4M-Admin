using System.IO;
using Microsoft.AspNetCore.Hosting;
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

        public static IWebHost BuildWebHost() =>
            new WebHostBuilder()
#if DEBUG
                .UseContentRoot(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\", "WebfrontCore")))
#else
                .UseContentRoot(Directory.GetCurrentDirectory())
#endif
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();
    }
}
