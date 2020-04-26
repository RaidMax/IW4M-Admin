using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationTests.Fixtures
{
    public class ClientGenerators
    {
        public static EFClient CreateBasicClient(Server currentServer, bool isIngame = true, bool hasIp = true, EFClient.ClientState clientState = EFClient.ClientState.Connected) => new EFClient()
        {
            ClientId = 1,
            CurrentAlias = new EFAlias()
            {
                Name = "BasicClient",
                IPAddress = hasIp ? "127.0.0.1".ConvertToIP() : null,
            },
            Level = EFClient.Permission.User,
            ClientNumber = isIngame ? 0 : -1,
            CurrentServer = currentServer
        };

        public static EFClient CreateDatabaseClient(bool hasIp = true) => new EFClient()
        {
            ClientId = 1,
            ClientNumber = -1,
            AliasLinkId = 1,
            Level = EFClient.Permission.User,
            Connections = 1,
            FirstConnection = DateTime.UtcNow.AddDays(-1),
            LastConnection = DateTime.UtcNow.AddMinutes(-5),
            NetworkId = 1,
            TotalConnectionTime = 100,
            CurrentAlias = new EFAlias()
            {
                Name = "BasicDatabaseClient",
                IPAddress = hasIp ? "127.0.0.1".ConvertToIP() : null,
            },
        };
    }
}
