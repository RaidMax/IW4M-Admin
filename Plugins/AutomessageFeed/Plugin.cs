using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;
using Microsoft.SyndicationFeed.Rss;
using System.Xml;
using Microsoft.SyndicationFeed;
using System.Collections.Generic;
using SharedLibraryCore.Helpers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Interfaces.Events;

namespace IW4MAdmin.Plugins.AutoMessageFeed;

public class Plugin : IPluginV2
{
    public string Name => "Automessage Feed";
    public string Version => Utilities.GetVersionAsString();
    public string Author => "RaidMax";

    private int _currentFeedItem;
    private readonly AutoMessageFeedConfiguration _configuration;

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<AutoMessageFeedConfiguration>("AutomessageFeedPluginSettings");
    }

    public Plugin(AutoMessageFeedConfiguration configuration)
    {
        _configuration = configuration;

        if (configuration.EnableFeed)
        {
            IManagementEventSubscriptions.Load += (manager, _) =>
            {
                manager.GetMessageTokens().Add(new MessageToken("FEED", GetNextFeedItem));
                return Task.CompletedTask;
            };
        }
    }

    private async Task<string> GetNextFeedItem(Server server)
    {
        if (!_configuration.EnableFeed)
        {
            return null;
        }
            
        var items = new List<string>();

        using (var reader = XmlReader.Create(_configuration.FeedUrl, new XmlReaderSettings { Async = true }))
        {
            var feedReader = new RssFeedReader(reader);

            while (await feedReader.Read())
            {
                if (feedReader.ElementType != SyndicationElementType.Item)
                {
                    continue;
                }

                var item = await feedReader.ReadItem();
                items.Add(Regex.Replace(item.Title, @"\<.+\>.*\</.+\>", ""));
            }
        }

        if (_currentFeedItem < items.Count && (_configuration.MaxFeedItems == 0 || _currentFeedItem < _configuration.MaxFeedItems))
        {
            _currentFeedItem++;
            return items[_currentFeedItem - 1];
        }

        _currentFeedItem = 0;
        return Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_AUTOMESSAGEFEED_NO_ITEMS"];
    }
}
