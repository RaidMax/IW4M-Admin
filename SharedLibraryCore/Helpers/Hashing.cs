using SimpleCrypto;

namespace SharedLibraryCore.Helpers
{
    public class Hashing
    {
        /// <summary>
        ///     Generate password hash and salt
        /// </summary>
        /// <param name="password">plaintext password</param>
        /// <param name="saltStr">salt of password</param>
        /// <returns></returns>
        public static string[] Hash(string password, string saltStr = null)
        {
            string hash;
            string salt;
            var CryptoSvc = new PBKDF2();

            // generate new hash 
            if (saltStr == null)
            {
                hash = CryptoSvc.Compute(password);
                salt = CryptoSvc.Salt;
                return new[]
                {
                    hash,
                    salt
                };
            }

            hash = CryptoSvc.Compute(password, saltStr);
            return new[]
            {
                hash,
                ""
            };
        }
    }
}
