namespace SharedLibraryCore.Dtos;

public class TwoFactorSetupInfo
{
    public string Secret { get; set; }
    public string QrCodeUrl { get; set; }
    public string ManualEntryKey { get; set; }
}
