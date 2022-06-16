using Data.Models;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Helpers;

public class TokenIdentifier : ITokenIdentifier
{
    public long NetworkId { get; set; }
    public Reference.Game Game { get; set; }
    public string Token { get; set; }
}
