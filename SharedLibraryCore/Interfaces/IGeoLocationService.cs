using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.Interfaces;

public interface IGeoLocationService
{
    /// <summary>
    /// Locates the country of the address using the bundled local database
    /// </summary>
    Task<IGeoLocationResult> Locate(string address);

    /// <summary>
    /// Retrieves detailed information (city, region, ISP, timezone) about the address through the master API.
    /// This is a remote, rate-limited lookup, so use it for on-demand requests rather than per-player events.
    /// </summary>
    /// <returns>the details, or null when no information is available for the address</returns>
    Task<GeoLocationDetails> LocateDetailed(string address, CancellationToken token = default) =>
        Task.FromResult<GeoLocationDetails>(null);
}
