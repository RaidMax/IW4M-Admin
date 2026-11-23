using System.Text.Json.Serialization;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Dtos;

/// <summary>
/// Detailed IP information (location, network and timezone) in the ipwhois response format.
/// Returned by <see cref="IGeoLocationService.LocateDetailed"/>.
/// </summary>
public class GeoLocationDetails : IGeoLocationResult
{
    [JsonPropertyName("ip")] public string IpAddress { get; set; }
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("continent")] public string Continent { get; set; }
    [JsonPropertyName("continent_code")] public string ContinentCode { get; set; }
    [JsonPropertyName("country")] public string Country { get; set; }
    [JsonPropertyName("country_code")] public string CountryCode { get; set; }
    [JsonPropertyName("region")] public string Region { get; set; }
    [JsonPropertyName("city")] public string City { get; set; }
    [JsonPropertyName("latitude")] public double? Latitude { get; set; }
    [JsonPropertyName("longitude")] public double? Longitude { get; set; }
    [JsonPropertyName("asn")] public string ASN { get; set; }
    [JsonPropertyName("org")] public string Organization { get; set; }
    [JsonPropertyName("isp")] public string Isp { get; set; }
    /// <summary>IANA timezone id, e.g. "America/Chicago"</summary>
    [JsonPropertyName("timezone")] public string Timezone { get; set; }
    /// <summary>UTC offset, e.g. "-05:00"</summary>
    [JsonPropertyName("timezone_gmt")] public string TimezoneGmt { get; set; }
    [JsonPropertyName("timezone_gmtOffset")] public int? TimezoneGmtOffset { get; set; }
    /// <summary>Which data source answered the lookup</summary>
    [JsonPropertyName("source")] public string Source { get; set; }
}
