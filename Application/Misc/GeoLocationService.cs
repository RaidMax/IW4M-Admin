using System;
using System.Threading.Tasks;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Misc;

public class GeoLocationService : IGeoLocationService
{
    private readonly string _sourceAddress;
    
    public GeoLocationService(string sourceAddress)
    {
        _sourceAddress = sourceAddress;
    }
    
    public Task<IGeoLocationResult> Locate(string address)
    {
        CountryResponse country = null;
        
        try
        {
            using var reader = new DatabaseReader(_sourceAddress);
            reader.TryCountry(address, out country);
        }
        catch
        {
            // ignored
        }

        var response = new GeoLocationResult
        {
            Country = country?.Country.Name ?? "Unknown",
            CountryCode = country?.Country.IsoCode ?? ""
        };

        return Task.FromResult((IGeoLocationResult)response);
    }
}
