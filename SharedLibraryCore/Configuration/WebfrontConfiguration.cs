namespace SharedLibraryCore.Configuration
{
    public class WebfrontConfiguration
    {
        public string PrimaryColor { get; set; } = "#117ac0";
        public string SecondaryColor { get; set; } = "pink";
        public string ThemePreset { get; set; } = "minimal";
        public bool PreventUserCustomization { get; set; }
    }
}
