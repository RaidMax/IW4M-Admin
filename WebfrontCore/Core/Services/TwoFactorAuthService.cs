using Google.Authenticator;
using SharedLibraryCore.Configuration;

namespace WebfrontCore.Core.Services;

public interface ITwoFactorAuthService
{
    (string secret, string qrCodeUrl, string manualEntryKey) GenerateSetup(string email);
    bool Validate(string secret, string code);
    IEnumerable<string> GenerateBackupCodes(int count = 10);
}

public class TwoFactorAuthService(ApplicationConfiguration appConfig) : ITwoFactorAuthService
{
    private const string DefaultCommunityName = "Add your community info in IW4MAdminSettings.json";

    public (string secret, string qrCodeUrl, string manualEntryKey) GenerateSetup(string email)
    {
        var communityName = appConfig.CommunityInformation.Name == DefaultCommunityName
            ? null
            : appConfig.CommunityInformation.Name;

        var issuer = string.IsNullOrEmpty(communityName)
            ? "IW4MAdmin"
            : $"IW4MAdmin ({communityName})";

        var tfa = new TwoFactorAuthenticator();
        var secret = Guid.NewGuid().ToString().Replace("-", "")[..10]; // Simple secret generation
        var setupInfo = tfa.GenerateSetupCode(issuer, email, secret, false, 3);
        return (secret, setupInfo.QrCodeSetupImageUrl, setupInfo.ManualEntryKey);
    }

    public bool Validate(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(secret)) return false;
        var tfa = new TwoFactorAuthenticator();
        return tfa.ValidateTwoFactorPIN(secret, code);
    }

    public IEnumerable<string> GenerateBackupCodes(int count = 10)
    {
        var codes = new List<string>();
        for (var i = 0; i < count; i++)
        {
            codes.Add(Guid.NewGuid().ToString("N")[..8].ToUpper());
        }
        return codes;
    }
}
