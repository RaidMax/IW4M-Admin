using SharedLibraryCore.Helpers;

namespace SharedLibraryCore.Interfaces
{
    public interface ITokenAuthentication
    {
        /// <summary>
        ///     generates and returns a token for the given network id
        /// </summary>
        /// <param name="networkId">network id of the players to generate the token for</param>
        /// <returns>4 character string token</returns>
        TokenState GenerateNextToken(long networkId);

        /// <summary>
        ///     authorizes given token
        /// </summary>
        /// <param name="token">token to authorize</param>
        /// <returns>true if token authorized successfully, false otherwise</returns>
        bool AuthorizeToken(long networkId, string token);
    }
}