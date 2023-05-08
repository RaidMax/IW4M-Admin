using System;

namespace Stats.Config;

public class PerformanceBucketConfiguration
{
    public string Name { get; set; }
    public TimeSpan ClientMinPlayTime { get; set; } = TimeSpan.FromHours(3);
    public TimeSpan RankingExpiration { get; set; } = TimeSpan.FromDays(15);
}
