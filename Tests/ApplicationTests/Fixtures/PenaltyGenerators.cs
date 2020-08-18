using SharedLibraryCore.Database.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationTests.Fixtures
{
    class PenaltyGenerators
    {
        public static EFPenalty Create(EFPenalty.PenaltyType type = EFPenalty.PenaltyType.Ban, EFClient originClient = null, EFClient targetClient = null, DateTime? occurs = null, string reason = null)
        {
            originClient ??= ClientGenerators.CreateDatabaseClient(clientId: 1);
            targetClient ??= ClientGenerators.CreateDatabaseClient(clientId: 2);
            occurs ??= DateTime.UtcNow;
            reason ??= "test";

            return new EFPenalty()
            {
                Offender = targetClient,
                Punisher = originClient,
                When = occurs.Value,
                Offense = reason,
                Type = type,
                LinkId = targetClient.AliasLinkId
            };
        }
    }
}
