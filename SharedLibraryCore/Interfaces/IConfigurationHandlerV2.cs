using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces;

public interface IConfigurationHandlerV2<TConfigurationType> where TConfigurationType: class
{
    Task<TConfigurationType> Get(string configurationName, TConfigurationType defaultConfiguration = null);
    Task Set(TConfigurationType configuration);
    Task Set();
}
