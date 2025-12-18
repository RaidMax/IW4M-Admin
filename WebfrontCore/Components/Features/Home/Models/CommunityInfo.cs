namespace WebfrontCore.Components.Features.Home.Models;

public class CommunityInfo
{
    public bool IsEnabled { get; set; }
    public required SocialAccountInfo[] SocialAccounts { get; set; }
}
