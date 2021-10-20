using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore.Internal;
using SharedLibraryCore.Dtos;
using Stats.Dtos;

namespace IW4MAdmin.Plugins.Stats
{
    public static class Extensions
    {
        private const int ZScoreRange = 3;
        private const int RankIconDivisions = 24;
        private const int MaxMessages = 100;

        public class LogParams
        {
            public double Mean { get; set; }
            public double Sigma { get; set; }
        }

        public static DateTime FifteenDaysAgo() => DateTime.UtcNow.AddDays(-15);

        public static double? WeightValueByPlaytime(this IEnumerable<EFClientStatistics> stats, string propertyName, 
            int minTimePlayed, Func<EFClientStatistics, bool> validation = null)
        {
            if (!stats.Any())
            {
                return null;
            }

            validation ??= (item) => item.Performance > 0 && item.TimePlayed >= minTimePlayed;

            var items = stats.Where(validation).ToList();
            var performancePlayTime = items.Sum(s => s.TimePlayed);

            var propInfo = typeof(EFClientStatistics).GetProperty(propertyName);
            var weightedValues = items.Sum(item =>
                (double?) propInfo?.GetValue(item) * (item.TimePlayed / (double) performancePlayTime));
            return weightedValues.Equals(double.NaN) ? 0 : weightedValues ?? 0;
        }

        public static LogParams GenerateDistributionParameters(this IEnumerable<double> values)
        {
            if (!values.Any())
            {
                return new LogParams()
                {
                    Mean = 0,
                    Sigma = 0
                };
            }
            
            var ti = 0.0;
            var ti2 = 0.0;
            var n = 0L;

            foreach (var val in values)
            {
                var logVal = Math.Log(val);
                ti += logVal * logVal;
                ti2 += logVal;
                n++;
                if (n % 50 == 0) // this isn't ideal, but we want to reduce the amount of CPU usage that the 
                    // loops takes so people don't complain
                {
                    Thread.Sleep(1);
                }
            }

            var mean = ti2 / n;
            ti2 *= ti2;
            var bottom = n == 1 ? 1 : n * (n - 1);
            var sigma = Math.Sqrt(((n * ti) - ti2) / bottom);

            return new LogParams()
            {
                Sigma = sigma,
                Mean = mean
            };
        }

        public static double? GetRatingForZScore(this double? zScore, double maxZScore)
        {
            const int ratingScalar = 1000;

            if (!zScore.HasValue)
            {
                return null;
            }

            // we just want everything positive so we can go from 0-max
            var adjustedZScore = zScore < -ZScoreRange ? 0 : zScore + ZScoreRange;
            return adjustedZScore / (maxZScore + ZScoreRange) * ratingScalar;
        }

        public static int RankIconIndexForZScore(this double? zScore)
        {
            if (zScore == null)
            {
                return 0;
            }

            const double divisionIncrement = (ZScoreRange * 2) / (double) RankIconDivisions;
            var rank = 1;
            for (var i = rank; i <= RankIconDivisions; i++)
            {
                var bottom = Math.Round(-ZScoreRange + (i - 1) * divisionIncrement, 5);
                var top = Math.Round(-ZScoreRange + i * divisionIncrement, 5);

                if (zScore > bottom && zScore <= top)
                {
                    return rank;
                }

                if (i == 1 && zScore < bottom // catch all for really bad players
                    // catch all for very good players
                    || i == RankIconDivisions && zScore > top)
                {
                    return i;
                }

                rank++;
            }

            return 0;
        }

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
            searchRequest.Count = Math.Min(searchRequest.Count, MaxMessages);
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