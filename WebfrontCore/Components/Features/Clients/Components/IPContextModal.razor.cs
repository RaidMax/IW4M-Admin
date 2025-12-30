using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Components;

public partial class IPContextModal
{
    [Inject] public required IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] public required ITranslationLookup Localization { get; set; }
    [Parameter] public string IPAddress { get; set; }
    private IpWhoisResponse? _geoData;
    private bool _isLoading = true;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        if (string.IsNullOrEmpty(IPAddress))
        {
            _isLoading = false;
            return;
        }

        try
        {
            using var http = HttpClientFactory.CreateClient();
            var response = await http.GetStringAsync($"https://ipwhois.app/json/{IPAddress}");
            _geoData = JsonSerializer.Deserialize<IpWhoisResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _error = $"Failed to lookup IP: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string GetLocationString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(_geoData?.City)) parts.Add(_geoData.City);
        if (!string.IsNullOrEmpty(_geoData?.Region)) parts.Add(_geoData.Region);
        if (!string.IsNullOrEmpty(_geoData?.Country)) parts.Add(_geoData.Country);
        return string.Join(", ", parts);
    }

    private class IpWhoisResponse
    {
        public string Isp { get; set; }
        public string Org { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        [JsonPropertyName("Timezone_Gmt")] public string TimezoneGmt { get; set; }
    }
}
