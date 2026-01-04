namespace SharedLibraryCore.Configuration;

public class WebfrontConfiguration
{
    public bool Enabled { get; set; }
    public string BindUrl { get; set; } = "http://0.0.0.0:1624";
    public string ManualUrl { get; set; }
    public string CustomBranding { get; set; }
    public bool EnableConnectionWhitelist { get; set; }
    public string[] ConnectionWhitelist { get; set; } = [];
    public string PrimaryColor { get; set; } = "#117ac0";
    public string SecondaryColor { get; set; } = "pink";
    public string ThemePreset { get; set; } = "minimal";
    public bool PreventUserCustomization { get; set; }
}
