namespace WebfrontCore.Components.Features.Auth.Models;

public class LoginRequest
{
    public required int ClientId { get; set; }
    public required string Password { get; set; }
}
