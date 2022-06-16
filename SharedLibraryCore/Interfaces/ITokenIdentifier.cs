namespace SharedLibraryCore.Interfaces;

public interface ITokenIdentifier
{
    int ClientId { get; }
    string Token { get; }
}
