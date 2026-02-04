namespace WebfrontCore.Components.Features.Auth.Models;

public class TwoFactorConfirmRequest
{
    public required string Secret { get; set; }
    public required string Code { get; set; }
}
