using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationTests.Fixtures
{
    public class ClientGenerators
    {
        public static EFClient CreateBasicClient(Server currentServer, bool isIngame = true) => new EFClient()
        {
            ClientId = 1,
            CurrentAlias = new EFAlias()
            {
                Name = "BasicClient",
                IPAddress = "127.0.0.1".ConvertToIP(),
            },
            Level = EFClient.Permission.User,
            ClientNumber = isIngame ? 0 : -1,
            CurrentServer = currentServer
        };
    }
}
