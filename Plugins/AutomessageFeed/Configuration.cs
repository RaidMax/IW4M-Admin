using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutomessageFeed
{
    class Configuration : IBaseConfiguration
    {
        public bool EnableFeed { get; set; }
        public string FeedUrl { get; set; }

        public IBaseConfiguration Generate()
        {
            EnableFeed = Utilities.PromptBool(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_AUTOMESSAGEFEED_PROMPT_ENABLE"]);

            if (EnableFeed)
            {
                FeedUrl = Utilities.PromptString(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_AUTOMESSAGEFEED_URL"]);
            }

            return this;
        }

        public string Name() => "AutomessageFeedConfiguration";
    }
}
