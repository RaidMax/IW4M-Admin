using System.Threading.Tasks;

namespace Stats.Client.Abstractions
{
    public interface IServerDistributionCalculator
    {
        Task Initialize();
        Task<double> GetZScoreForServerOrBucket(double value, long? serverId = null, string performanceBucket = null);
        Task<double?> GetRatingForZScore(double? value, string performanceBucket);
    }
}
