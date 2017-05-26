using System;
using System.Security.Cryptography;
using System.Text;
 
//http://codereview.stackexchange.com/questions/96494/user-password-encryption-in-c + SCrypt
namespace MessageBoard.Encryption
{

    public static class PasswordHasher
    {
        public static byte[] ComputeHash(string password, byte[] salt)
        { 
            byte[] pwBytes = Encoding.UTF8.GetBytes(password);
            byte[] hashBytes = new byte[64];
            CryptSharp.Utility.SCrypt.ComputeKey(pwBytes, salt, 16384, 8, 1, null, hashBytes);
            return hashBytes;
        }

        public static byte[] GenerateSalt(int saltByteSize = 24)
        {
            RNGCryptoServiceProvider saltGenerator = new RNGCryptoServiceProvider();
            byte[] salt = new byte[saltByteSize];
            saltGenerator.GetBytes(salt);
            return salt;
        }

        public static bool VerifyPassword(String password, byte[] passwordSalt, byte[] passwordHash)
        {
            byte[] computedHash = ComputeHash(password, passwordSalt);
            return AreHashesEqual(computedHash, passwordHash);
        }

        //Length constant verification - prevents timing attack
        private static bool AreHashesEqual(byte[] firstHash, byte[] secondHash)
        {
            int minHashLength = firstHash.Length <= secondHash.Length ? firstHash.Length : secondHash.Length;
            var xor = firstHash.Length ^ secondHash.Length;
            for (int i = 0; i < minHashLength; i++)
                xor |= firstHash[i] ^ secondHash[i];
            return 0 == xor;
        }
    }
}