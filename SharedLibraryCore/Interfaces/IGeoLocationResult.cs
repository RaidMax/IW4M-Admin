namespace SharedLibraryCore.Interfaces;

public interface IGeoLocationResult
{
    string Country { get; set; }
    string CountryCode { get; set; }
    string Region { get; set; }
    string ASN { get; set; }
    string Timezone { get; set; }
    string Organization { get; set; }
}
