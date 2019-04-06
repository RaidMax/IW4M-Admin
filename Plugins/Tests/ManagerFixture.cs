using IW4MAdmin.Application;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tests
{
    public class ManagerFixture : IDisposable
    {
        public ApplicationManager Manager { get; private set; }

        public ManagerFixture()
        {
            File.WriteAllText("test_mp.log", "test_log_file");

            //IW4MAdmin.Application.Localization.Configure.Initialize("en-US");

            Manager = ApplicationManager.GetInstance();

            var config = new ApplicationConfiguration
            {
                Servers = new List<ServerConfiguration>()
                {
                    new ServerConfiguration()
                    {
                        AutoMessages = new List<string>(),
                        IPAddress = "127.0.0.1",
                        Password = "test",
                        Port = 28963,
                        Rules = new List<string>(),
                        ManualLogPath = "http://google.com"
                    }
                },
                AutoMessages = new List<string>(),
                GlobalRules = new List<string>(),
                Maps = new List<MapConfiguration>(),
                RConPollRate = 10000
            };
            Manager.ConfigHandler = new BaseConfigurationHandler<ApplicationConfiguration>("test");
            Manager.ConfigHandler.Set(config);

            Manager.Init().Wait();

            Task.Run(() => Manager.Start());
        }

        public void Dispose()
        {
            Manager.Stop();
        }
    }

    [CollectionDefinition("ManagerCollection")]
    public class ManagerCollection : ICollectionFixture<ManagerFixture>
    {

    }
}
