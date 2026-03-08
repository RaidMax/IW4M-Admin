using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SharedLibraryCore.Configuration;
using WebfrontCore.Components.UI.Theme.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.UI.Theme;

public partial class ThemeSelector
{
    [Inject] public IJSRuntime JSRuntime { get; set; }
    [Inject] public NavigationManager NavigationManager { get; set; }
    [Inject] public ApplicationConfiguration AppConfig { get; set; }
    [Inject] public AppState AppState { get; set; }
    private bool _disposed;
    private bool _isOpen;
    private string _preset;
    private string _primaryPalette;
    private int _primaryHue;
    private int _primarySaturation;
    private int _primaryLightness;
    private string _secondaryPalette;
    private int _secondaryHue;
    private int _secondarySaturation;
    private int _secondaryLightness;

    // Computed defaults from server config
    private string DefaultPreset => AppConfig?.Webfront?.ThemePreset ?? "minimal";
    private string DefaultPrimaryColor => AppConfig?.Webfront?.PrimaryColor ?? "blue";
    private string DefaultSecondaryColor => AppConfig?.Webfront?.SecondaryColor ?? "purple";

    // Computed hex values from current HSL
    private string PrimaryHex => HslToHex(_primaryHue, _primarySaturation, _primaryLightness);
    private string SecondaryHex => HslToHex(_secondaryHue, _secondarySaturation, _secondaryLightness);

    private static readonly string[] AvailablePalettes = new[]
    {
        "blue", "sky", "cyan", "teal", "emerald", "green", "lime", "yellow",
        "amber", "orange", "red", "rose", "pink", "fuchsia", "purple", "violet",
        "indigo", "slate", "gray", "zinc", "neutral", "stone"
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadFromStorage();
            await ApplyTheme();
            NavigationManager.LocationChanged += OnLocationChanged;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async void OnLocationChanged(object? sender,
        Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await ApplyTheme();
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        _disposed = true;
        NavigationManager.LocationChanged -= OnLocationChanged;
    }

    private void ToggleDropdown() => _isOpen = !_isOpen;

    private string GetPresetButtonClass(string preset) =>
        _preset == preset
            ? "bg-action-primary text-foreground"
            : "bg-action-secondary text-foreground hover:bg-action-secondary-hover";

    private async Task SetPreset(string preset)
    {
        _preset = preset;
        await ApplyTheme();
    }

    private async Task OnPrimaryChange(ChangeEventArgs e)
    {
        _primaryPalette = e.Value?.ToString() ?? DefaultPrimaryColor;
        await ApplyTheme();
    }

    private async Task OnSecondaryChange(ChangeEventArgs e)
    {
        _secondaryPalette = e.Value?.ToString() ?? DefaultSecondaryColor;
        await ApplyTheme();
    }

    private async Task OnPrimaryHueChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
        {
            _primaryHue = v;
            await ApplyTheme();
        }
    }

    private async Task OnPrimarySaturationChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
        {
            _primarySaturation = v;
            await ApplyTheme();
        }
    }

    private async Task OnPrimaryLightnessChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
        {
            _primaryLightness = v;
            await ApplyTheme();
        }
    }

    private async Task OnSecondaryHueChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
        {
            _secondaryHue = v;
            await ApplyTheme();
        }
    }

    private async Task OnSecondarySaturationChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
        {
            _secondarySaturation = v;
            await ApplyTheme();
        }
    }

    private async Task OnSecondaryLightnessChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
        {
            _secondaryLightness = v;
            await ApplyTheme();
        }
    }

    private async Task OnPrimaryHexChange(ChangeEventArgs e)
    {
        var hex = e.Value?.ToString()?.Trim() ?? "";
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (hex.Length == 7 || hex.Length == 4)
        {
            // Expand shorthand (e.g., #abc -> #aabbcc)
            if (hex.Length == 4)
            {
                hex = $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";
            }
            var hsl = ParseHexToHSL(hex);
            _primaryHue = hsl.h;
            _primarySaturation = hsl.s;
            _primaryLightness = hsl.l;
            await ApplyTheme();
        }
    }

    private async Task OnSecondaryHexChange(ChangeEventArgs e)
    {
        var hex = e.Value?.ToString()?.Trim() ?? "";
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (hex.Length == 7 || hex.Length == 4)
        {
            // Expand shorthand (e.g., #abc -> #aabbcc)
            if (hex.Length == 4)
            {
                hex = $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";
            }
            var hsl = ParseHexToHSL(hex);
            _secondaryHue = hsl.h;
            _secondarySaturation = hsl.s;
            _secondaryLightness = hsl.l;
            await ApplyTheme();
        }
    }

    private async Task ApplyTheme()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var settings = new
            {
                preset = _preset,
                primaryColorMode = _primaryPalette == "custom" ? 0 : 1,
                primaryPalette = _primaryPalette,
                primaryHue = _primaryHue,
                primarySaturation = _primarySaturation,
                primaryLightness = _primaryLightness,
                secondaryColorMode = _secondaryPalette == "custom" ? 0 : 1,
                secondaryPalette = _secondaryPalette,
                secondaryHue = _secondaryHue,
                secondarySaturation = _secondarySaturation,
                secondaryLightness = _secondaryLightness
            };

            // Save BEFORE apply to prevent MutationObserver race condition
            // (observer loads from localStorage and would revert to old settings)
            if (!AppConfig.Webfront.PreventUserCustomization)
            {
                await JSRuntime.InvokeVoidAsync("themeManager.save", settings);
            }

            await JSRuntime.InvokeVoidAsync("themeManager.apply", settings);
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task ResetToDefaults()
    {
        ApplyServerDefaults();
        await ApplyTheme();
    }

    private void ApplyServerDefaults()
    {
        _preset = DefaultPreset;

        var primary = DefaultPrimaryColor;
        var isCustomPrimary = primary.StartsWith("#") || primary.StartsWith("hsl") || primary.StartsWith("rgb");
        _primaryPalette = isCustomPrimary ? "custom" : primary.ToLowerInvariant();

        if (isCustomPrimary && primary.StartsWith("#"))
        {
            var hsl = ParseHexToHSL(primary);
            _primaryHue = hsl.h;
            _primarySaturation = hsl.s;
            _primaryLightness = hsl.l;
        }
        else
        {
            // Get HSL from palette lookup or use safe defaults
            var paletteHsl = GetPaletteHSL(primary);
            _primaryHue = paletteHsl.h;
            _primarySaturation = paletteHsl.s;
            _primaryLightness = paletteHsl.l;
        }

        var secondary = DefaultSecondaryColor;
        var isCustomSecondary = secondary.StartsWith("#") || secondary.StartsWith("hsl") || secondary.StartsWith("rgb");
        _secondaryPalette = isCustomSecondary ? "custom" : secondary.ToLowerInvariant();

        if (isCustomSecondary && secondary.StartsWith("#"))
        {
            var hsl = ParseHexToHSL(secondary);
            _secondaryHue = hsl.h;
            _secondarySaturation = hsl.s;
            _secondaryLightness = hsl.l;
        }
        else
        {
            // Get HSL from palette lookup or use safe defaults
            var paletteHsl = GetPaletteHSL(secondary);
            _secondaryHue = paletteHsl.h;
            _secondarySaturation = paletteHsl.s;
            _secondaryLightness = paletteHsl.l;
        }
    }

    private static (int h, int s, int l) GetPaletteHSL(string palette)
    {
        return palette?.ToLowerInvariant() switch
        {
            "blue" => (217, 91, 60),
            "sky" => (199, 89, 48),
            "cyan" => (188, 94, 43),
            "teal" => (168, 76, 42),
            "emerald" => (160, 84, 39),
            "green" => (142, 71, 45),
            "lime" => (84, 81, 44),
            "yellow" => (48, 96, 53),
            "amber" => (38, 92, 50),
            "orange" => (25, 95, 53),
            "red" => (0, 84, 60),
            "rose" => (347, 77, 50),
            "pink" => (330, 81, 60),
            "fuchsia" => (292, 84, 61),
            "purple" => (271, 91, 65),
            "violet" => (258, 90, 66),
            "indigo" => (239, 84, 67),
            "slate" => (215, 16, 47),
            "gray" => (220, 9, 46),
            "zinc" => (240, 5, 46),
            "neutral" => (0, 0, 45),
            "stone" => (25, 6, 45),
            _ => (217, 91, 60) // fallback to blue
        };
    }

    private (int h, int s, int l) ParseHexToHSL(string hex)
    {
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);
        if (hex.Length != 6)
            return (0, 0, 0);

        var r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        var g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        var b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        float h, s, l = (max + min) / 2f;

        if (max == min)
        {
            h = s = 0; // achromatic
        }
        else
        {
            var d = max - min;
            s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

            if (max == r)
                h = (g - b) / d + (g < b ? 6f : 0f);
            else if (max == g)
                h = (b - r) / d + 2f;
            else
                h = (r - g) / d + 4f;

            h /= 6f;
        }

        return ((int)Math.Round(h * 360f), (int)Math.Round(s * 100f), (int)Math.Round(l * 100f));
    }

    private static string HslToHex(int h, int s, int l)
    {
        var hNorm = h / 360f;
        var sNorm = s / 100f;
        var lNorm = l / 100f;

        float r, g, b;

        if (sNorm == 0)
        {
            r = g = b = lNorm; // achromatic
        }
        else
        {
            var q = lNorm < 0.5f ? lNorm * (1 + sNorm) : lNorm + sNorm - lNorm * sNorm;
            var p = 2 * lNorm - q;
            r = HueToRgb(p, q, hNorm + 1f / 3f);
            g = HueToRgb(p, q, hNorm);
            b = HueToRgb(p, q, hNorm - 1f / 3f);
        }

        var rInt = (int)Math.Round(r * 255);
        var gInt = (int)Math.Round(g * 255);
        var bInt = (int)Math.Round(b * 255);

        return $"#{rInt:X2}{gInt:X2}{bInt:X2}";
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1f / 6f) return p + (q - p) * 6 * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6;
        return p;
    }

    private async Task LoadFromStorage()
    {
        // If locked, maximize enforcement by ignoring local storage
        if (AppConfig.Webfront.PreventUserCustomization)
        {
            ApplyServerDefaults();
            return;
        }

        try
        {
            var settings = await JSRuntime.InvokeAsync<ThemeSettings>("themeManager.load");
            if (settings != null)
            {
                _preset = settings.Preset ?? DefaultPreset;
                _primaryPalette = settings.PrimaryColorMode == 0 ? "custom" : (settings.PrimaryPalette ?? DefaultPrimaryColor);
                _primaryHue = settings.PrimaryHue;
                _primarySaturation = settings.PrimarySaturation;
                _primaryLightness = settings.PrimaryLightness;

                _secondaryPalette = settings.SecondaryColorMode == 0
                    ? "custom"
                    : (settings.SecondaryPalette ?? DefaultSecondaryColor);
                _secondaryHue = settings.SecondaryHue;
                _secondarySaturation = settings.SecondarySaturation;
                _secondaryLightness = settings.SecondaryLightness;
            }
            else
            {
                ApplyServerDefaults();
            }
        }
        catch
        {
            ApplyServerDefaults();
        }
    }
}
