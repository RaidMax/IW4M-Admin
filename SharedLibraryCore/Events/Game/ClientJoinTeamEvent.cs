using System;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Events.Game;

public class ClientJoinTeamEvent : ClientGameEvent
{
    public string TeamName { get; init; }
    public EFClient.TeamType? Team {
        get
        {
            if (Enum.TryParse(typeof(EFClient.TeamType), TeamName, out var parsedTeam) && parsedTeam is not null)
            {
                return (EFClient.TeamType)parsedTeam;
            }

            return null;
        }
    }
}
