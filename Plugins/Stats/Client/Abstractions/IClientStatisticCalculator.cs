using System.Threading.Tasks;
using SharedLibraryCore;

namespace IW4MAdmin.Plugins.Stats.Client.Abstractions
{
    public interface IClientStatisticCalculator
    {
        Task GatherDependencies();
        Task CalculateForEvent(GameEvent gameEvent);
    }
}