using SharedLibraryCore.Dtos;

namespace WebfrontCore.Components.UI.Navigation.Models;

public class NavigationInfo
{
    public ClientInfo? User { get; set; }
    public bool Authorized { get; set; }
    public required IEnumerable<Page> Pages { get; set; }
    public required IEnumerable<NavigationInteractionInfo> Interactions { get; set; }
    public required WebfrontCore.Components.Features.Home.Models.CommunityInfo CommunityInformation { get; set; }
    public int TotalClientCount { get; set; }
    public int? TotalAdminCount { get; set; }
    public int? TotalReportCount { get; set; }
    public int? TotalFlaggedCount { get; set; }
}
