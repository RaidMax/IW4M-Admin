using System.Threading.Tasks;

namespace Stats.Client.Abstractions
{
    public interface IServerDistributionCalculator
    {
        Task Initialize();
        Task<double> GetZScoreForServer(long serverId, double value);
        Task<double?> GetRatingForZScore(double? value);
    }
}