using SharedLibraryCore.Helpers;

namespace SharedLibraryCore.Interfaces
{
    public interface ITokenAuthentication
    {
        /// <summary>
        ///     generates and returns a token for the given network id
        /// </summary>
        /// <param name="authInfo">auth information for next token generation</param>
        /// <returns>4 character string token</returns>
        TokenState GenerateNextToken(ITokenIdentifier authInfo);

        /// <summary>
        ///     authorizes given token
        /// </summary>
        /// <param name="authInfo">auth information</param>
        /// <returns>true if token authorized successfully, false otherwise</returns>
        bool AuthorizeToken(ITokenIdentifier authInfo);
    }
}
