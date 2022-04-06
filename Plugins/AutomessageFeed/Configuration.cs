using SharedLibraryCore.Interfaces;

namespace AutomessageFeed
{
    class Configuration : IBaseConfiguration
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
}
