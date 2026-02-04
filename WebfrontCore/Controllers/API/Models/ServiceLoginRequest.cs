namespace WebfrontCore.Controllers.API.Models;

public class ServiceLoginRequest
{
    public int ClientId { get; set; }
    public required string Password { get; set; }
    public required string IpAddress { get; set; }
    public string? TwoFactorCode { get; set; }
}
