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
            string logFile = @"X:\IW4MAdmin\Plugins\Tests\bin\Debug\netcoreapp2.2\test_mp.log";

            File.WriteAllText(logFile, Environment.NewLine);

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
                        Port = 28960,
                        Rules = new List<string>(),
                        RConParserVersion = "test",
                        EventParserVersion = "IW4x (v0.6.0)",
                        ManualLogPath = logFile
                    }
                },
                AutoMessages = new List<string>(),
                GlobalRules = new List<string>(),
                Maps = new List<MapConfiguration>(),
                RConPollRate = int.MaxValue
            };

            Manager.ConfigHandler = new BaseConfigurationHandler<ApplicationConfiguration>("test");
            Manager.ConfigHandler.Set(config);
            Manager.AdditionalRConParsers.Add(new TestRconParser());

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
