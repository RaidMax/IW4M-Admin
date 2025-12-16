namespace WebfrontCore.ViewModels;

public class LoginRequest
{
    public required int ClientId { get; set; }
    public required string Password { get; set; }
}
