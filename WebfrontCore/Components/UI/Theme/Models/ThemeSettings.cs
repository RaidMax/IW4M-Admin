using System.Text.Json.Serialization;

namespace WebfrontCore.Components.UI.Theme.Models;

internal class ThemeSettings
{
    [JsonPropertyName("preset")] public string? Preset { get; set; }

    [JsonPropertyName("primaryColorMode")] public int PrimaryColorMode { get; set; }

    [JsonPropertyName("primaryPalette")] public string? PrimaryPalette { get; set; }

    [JsonPropertyName("primaryHue")] public int PrimaryHue { get; set; }

    [JsonPropertyName("primarySaturation")]
    public int PrimarySaturation { get; set; }

    [JsonPropertyName("primaryLightness")] public int PrimaryLightness { get; set; }

    [JsonPropertyName("secondaryColorMode")]
    public int SecondaryColorMode { get; set; }

    [JsonPropertyName("secondaryPalette")] public string? SecondaryPalette { get; set; }

    [JsonPropertyName("secondaryHue")] public int SecondaryHue { get; set; }

    [JsonPropertyName("secondarySaturation")]
    public int SecondarySaturation { get; set; }

    [JsonPropertyName("secondaryLightness")]
    public int SecondaryLightness { get; set; }
}
