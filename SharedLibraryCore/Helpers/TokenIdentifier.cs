using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Helpers;

public class TokenIdentifier : ITokenIdentifier
{
    public int ClientId { get; set; }
    public string Token { get; set; }
}
