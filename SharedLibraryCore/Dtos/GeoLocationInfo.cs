namespace SharedLibraryCore.Dtos
{
    /// <summary>
    /// Serializable DTO for geographic location information.
    /// Used in API responses where IGeoLocationResult cannot be deserialized.
    /// </summary>
    public class GeoLocationInfo
    {
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string Region { get; set; }
        public string ASN { get; set; }
        public string Timezone { get; set; }
        public string Organization { get; set; }
    }
}
