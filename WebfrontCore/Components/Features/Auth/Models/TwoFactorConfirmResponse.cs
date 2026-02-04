namespace WebfrontCore.Components.Features.Auth.Models;

public class TwoFactorConfirmResponse
{
    public bool Success { get; set; }
    public IEnumerable<string> BackupCodes { get; set; }
}
