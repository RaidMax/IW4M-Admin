using System;
using SharedLibraryCore;

namespace Stats.Config;

public class PerformanceBucketConfiguration
{
    public string FriendlyNameRENAME { get; set; }
    public string Code { get; set; }
    public TimeSpan ClientMinPlayTime { get; set; } = Utilities.IsDevelopment ? TimeSpan.FromMinutes(1) : TimeSpan.FromHours(3);
    public TimeSpan RankingExpiration { get; set; } = TimeSpan.FromDays(15);
}
