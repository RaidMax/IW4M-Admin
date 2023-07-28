using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ProtoBuf;

namespace Integrations.Cod.SecureRcon;

public static class Helpers
{
    private static byte[] ToSerializedMessage(this SecureCommand command)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, command);
        return ms.ToArray();
    }
    
    private static byte[] SignData(byte[] data, string privateKey)
    {
        using var rsa = new RSACryptoServiceProvider(512);
        rsa.ImportFromPem(privateKey);
        var rsaFormatter = new RSAPKCS1SignatureFormatter(rsa);
        rsaFormatter.SetHashAlgorithm("SHA512");
        var hash = SHA512.Create();
        var hashedData = hash.ComputeHash(data);
        var signature = rsaFormatter.CreateSignature(hashedData);

        return signature;
    }
    
    public static byte SafeConversion(char c)
    {
        try
        {
            return Convert.ToByte(c);
        }

        catch
        {
            return (byte)'.';
        }
    }

    public static byte[] BuildSafeRconPayload(string prefix, string command, string signingKey)
    {
        var message = command.Select(SafeConversion).ToArray();
        var header = (prefix + "\n").Select(SafeConversion).ToArray();

        var secureCommand = new SecureCommand
        {
            SecMessage = message,
            Signature = SignData(message, signingKey)
        };

        return header.Concat(secureCommand.ToSerializedMessage()).ToArray();
    }
}
