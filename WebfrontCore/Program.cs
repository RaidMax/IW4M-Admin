using System.IO;
using Microsoft.AspNetCore.Hosting;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore
{
    public class Program
    {
        public static IManager Manager;

        public static void Init(IManager mgr)
        {
            Manager = mgr;
            BuildWebHost().Run();
        }

        public static IWebHost BuildWebHost() =>
            new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();
    }
}
