using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace WebfrontCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
#if !DEBUG
                .UseUrls("http://server.nbsclan.org:8080")
#else
                .UseUrls("http://127.0.0.1:5000;http://192.168.88.254:5000")
#endif
                .Build();

            host.Run();
        }
    }
}
