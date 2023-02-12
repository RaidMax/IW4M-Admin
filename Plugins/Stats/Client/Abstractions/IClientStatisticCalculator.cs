using System.Threading.Tasks;
using SharedLibraryCore.Events;

namespace IW4MAdmin.Plugins.Stats.Client.Abstractions
{
    public interface IClientStatisticCalculator
    {
        Task GatherDependencies();
        Task CalculateForEvent(CoreEvent coreEvent);
    }
}
