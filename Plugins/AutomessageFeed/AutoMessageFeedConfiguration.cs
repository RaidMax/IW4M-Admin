using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.AutoMessageFeed;

public class AutoMessageFeedConfiguration : IBaseConfiguration
{
    public bool EnableFeed { get; set; }
    public string FeedUrl { get; set; }
    public int MaxFeedItems { get; set; }

    public IBaseConfiguration Generate()
    {
        return this;
    }

    public string Name() => "AutomessageFeedConfiguration";
}
