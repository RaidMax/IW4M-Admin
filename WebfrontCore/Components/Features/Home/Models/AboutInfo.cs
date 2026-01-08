using SharedLibraryCore.Configuration;

namespace WebfrontCore.Components.Features.Home.Models;

public class AboutInfo
{
    public required CommunityInformationConfiguration CommunityInformation { get; set; }
    public required string[] GlobalRules { get; set; }
    public required List<ServerRulesInfo> ServerRules { get; set; }
}
