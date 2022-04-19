using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces;

public interface IGeoLocationService
{
    Task<IGeoLocationResult> Locate(string address);
}
