using SharedLibraryCore.Database.Models;
using SharedLibraryCore.RCon;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    class TestRconParser : IW4MAdmin.Application.RconParsers.BaseRConParser
    {
        public int FakeClientCount { get; set; }
        public List<EFClient> FakeClients { get; set; } = new List<EFClient>();

        public override string Version => "test";

        public override async Task<(List<EFClient>, string)> GetStatusAsync(Connection connection)
        {
            var clientList = new List<EFClient>();
           
            for (int i = 0; i < FakeClientCount; i++)
            {
                clientList.Add(new EFClient()
                {
                    ClientNumber = i,
                    NetworkId = i + 1,
                    CurrentAlias = new EFAlias()
                    {
                        Name = $"test_bot_{i}",
                        IPAddress = i + 1
                    }
                });
            }

            return clientList.Count > 0 ? (clientList, "mp_rust") : (FakeClients, "mp_rust");
        }
    }
}
