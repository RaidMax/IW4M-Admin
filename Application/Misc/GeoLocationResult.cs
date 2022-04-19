using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Misc;

public class GeoLocationResult : IGeoLocationResult
{
    public string Country { get; set; }
    public string CountryCode { get; set; }
    public string Region { get; set; }
    public string ASN { get; set; }
    public string Timezone { get; set; }
    public string Organization { get; set; }
}
