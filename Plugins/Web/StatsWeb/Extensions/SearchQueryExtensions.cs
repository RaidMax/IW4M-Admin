using SharedLibraryCore.Dtos;
using StatsWeb.Dtos;
using System;
using System.Linq;

namespace StatsWeb.Extensions
{
    public static class SearchQueryExtensions
    {
        private const int MAX_MESSAGES = 100;

        /// <summary>
        ///  todo: lets abstract this out to a generic buildable query
        ///  this is just a dirty PoC
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static ChatSearchQuery ParseSearchInfo(this string query, int count, int offset)
        {
            string[] filters = query.Split('|');
            var searchRequest = new ChatSearchQuery
            {
                Filter = query,
                Count = count,
                Offset = offset
            };

            // sanity checks
            searchRequest.Count = Math.Min(searchRequest.Count, MAX_MESSAGES);
            searchRequest.Count = Math.Max(searchRequest.Count, 0);
            searchRequest.Offset = Math.Max(searchRequest.Offset, 0);

            if (filters.Length > 1)
            {
                if (filters[0].ToLower() != "chat")
                {
                    throw new ArgumentException("Query is not compatible with chat");
                }

                foreach (string filter in filters.Skip(1))
                {
                    string[] args = filter.Split(' ');

                    if (args.Length > 1)
                    {
                        string recombinedArgs = string.Join(' ', args.Skip(1));
                        switch (args[0].ToLower())
                        {
                            case "before":
                                searchRequest.SentBefore = DateTime.Parse(recombinedArgs);
                                break;
                            case "after":
                                searchRequest.SentAfter = DateTime.Parse(recombinedArgs);
                                break;
                            case "server":
                                searchRequest.ServerId = args[1];
                                break;
                            case "client":
                                searchRequest.ClientId = int.Parse(args[1]);
                                break;
                            case "contains":
                                searchRequest.MessageContains = string.Join(' ', args.Skip(1));
                                break;
                            case "sort":
                                searchRequest.Direction = Enum.Parse<SortDirection>(args[1], ignoreCase: true);
                                break;
                        }
                    }
                }

                return searchRequest;
            }

            throw new ArgumentException("No filters specified for chat search");
        }
    }
}
