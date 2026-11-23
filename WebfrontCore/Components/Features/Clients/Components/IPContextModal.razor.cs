using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;

namespace WebfrontCore.Components.Features.Clients.Components;

public partial class IPContextModal
{
    [Inject] public required IGeoLocationService GeoLocationService { get; set; }
    [Inject] public required ITranslationLookup Localization { get; set; }
    [Parameter, EditorRequired] public string IPAddress { get; set; } = default!;
    private GeoLocationDetails? _geoData;
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
            _geoData = await GeoLocationService.LocateDetailed(IPAddress);
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
}
