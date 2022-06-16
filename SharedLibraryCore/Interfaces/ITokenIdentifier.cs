
using Data.Models;

namespace SharedLibraryCore.Interfaces;

public interface ITokenIdentifier
{
    long NetworkId { get; }
    Reference.Game Game { get; set; }
    string Token { get; set; }
}
