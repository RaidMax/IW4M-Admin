using System.Security.Claims;
using SharedLibraryCore.Configuration;
using EFClient = SharedLibraryCore.Database.Models.EFClient;

namespace WebfrontCore.Core.Auth;

/// <summary>
/// The one place that decides which claims a webfront session carries. Every path that issues a session
/// cookie (form login, API login, refresh) must use it, so a constraint such as forced 2FA enrolment
/// cannot be skipped by choosing a different login endpoint.
/// </summary>
public static class WebfrontClaims
{
    public static List<Claim> For(EFClient client, ApplicationConfiguration configuration)
    {
        var hasTwoFactor = !string.IsNullOrEmpty(client.TwoFactorSecret);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, client.Name),
            new(ClaimTypes.Role, client.Level.ToString()),
            new(ClaimTypes.Sid, client.ClientId.ToString()),
            new(ClaimTypes.PrimarySid, client.NetworkId.ToString("X")),
            new(ClaimTypes.PrimaryGroupSid, client.GameName.ToString()),
            new(WebfrontClaimTypes.HasTwoFactor, hasTwoFactor.ToString().ToLower())
        };

        if (configuration.Webfront.RequireTwoFactorForPrivilegedClients &&
            client.Level >= Data.Models.Client.EFClient.Permission.Moderator &&
            !hasTwoFactor)
        {
            claims.Add(new Claim(WebfrontClaimTypes.PendingTwoFactorEnrollment, "true"));
        }

        return claims;
    }
}
