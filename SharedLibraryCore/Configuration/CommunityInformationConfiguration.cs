namespace SharedLibraryCore.Configuration;

public class CommunityInformationConfiguration
{
    public string Name { get; set; } = "Add your community info in IW4MAdminSettings.json";

    public string Tagline { get; set; } = "The complete administration tool Call of Duty® dedicated servers";

    public string Description { get; set; } =
        "IW4MAdmin is an administration tool for IW4x, Pluto T6, Pluto IW5, CoD4x, TeknoMW3, and most Call of Duty® dedicated servers. It allows complete control of your server; from changing maps, to banning players, IW4MAdmin monitors and records activity on your server(s). With plugin support, extending its functionality is a breeze.";

    public SocialAccountConfiguration[] SocialAccounts { get; set; } =
    [
        new()
        {
            Title = "IW4MAdmin Website",
            Url = "https://raidmax.org/IW4MAdmin",
            IconId = "ph-globe-simple"
        },
        new()
        {
            Title = "IW4MAdmin Github",
            Url = "https://github.com/RaidMax/IW4M-Admin/",
            IconUrl = "github.svg"
        },
        new()
        {
            Title = "IW4MAdmin Youtube",
            Url = "https://www.youtube.com/watch?v=xpxEO4Qi0cQ",
            IconUrl = "https://raw.githubusercontent.com/edent/SuperTinyIcons/master/images/svg/youtube.svg"
        }
    ];

    public bool IsEnabled { get; set; } = true;
}

public class SocialAccountConfiguration
{
    public string Title { get; set; }
    public string Url { get; set; }
    public string IconUrl { get; set; }
    public string IconId { get; set; }
}
