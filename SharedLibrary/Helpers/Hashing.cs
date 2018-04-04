using System;
using SimpleCrypto;

namespace SharedLibrary.Helpers
{
    public class Hashing
    {
        /// <summary>
        /// Generate password hash and salt
        /// </summary>
        /// <param name="password">plaintext password</param>
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
                return new string[]
                {
                    hash,
                    salt
                };
            }

            else
            {
                hash = CryptoSvc.Compute(password, saltStr);
                return new string[]
                {
                    hash,
                    ""
                };
            }



            /*//https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing

            byte[] salt;

            if (saltStr == null)
            {
                salt = new byte[128 / 8];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }
            }

            else
            {
                salt = Convert.FromBase64String(saltStr);
            }

            // derive a 256-bit subkey (use HMACSHA1 with 10,000 iterations)
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            return new string[] 
            {
                hashed,
                Convert.ToBase64String(salt)
            };*/
        }
    }
}
