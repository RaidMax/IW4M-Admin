using SharedLibraryCore;
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
            EnableFeed = Utilities.PromptBool(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_AUTOMESSAGEFEED_PROMPT_ENABLE"]);

            if (EnableFeed)
            {
                FeedUrl = Utilities.PromptString(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_AUTOMESSAGEFEED_URL"]);
                MaxFeedItems = Utilities.PromptInt(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_AUTOMESSAGEFEED_PROMPT_MAXITEMS"],
                    Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_AUTOMESSAGEFEED_PROMPT_MAXITEMS_DESC"],
                    0, int.MaxValue, 0);
            }

            return this;
        }

        public string Name() => "AutomessageFeedConfiguration";
    }
}
