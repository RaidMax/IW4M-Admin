using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc
{
    public class RemoteAssemblyHandler(ILogger<RemoteAssemblyHandler> logger, ApplicationConfiguration appconfig)
        : IRemoteAssemblyHandler
    {
        private const int KeyLength = 32;
        private const int TagLength = 16;
        private const int NonceLength = 12;
        private const int IterationCount = 10000;

        private readonly ILogger _logger = logger;

        public IEnumerable<Assembly> DecryptAssemblies(string[] encryptedAssemblies)
        {
            var assemblies = new List<Assembly>();

            foreach (var assemblyBytes in DecryptContent(encryptedAssemblies))
            {
                try
                {
                    assemblies.Add(Assembly.Load(assemblyBytes));
                }
                finally
                {
                    // Assembly.Load copies the image into the runtime's own memory, so the
                    // decrypted plaintext PE in this buffer is no longer needed once it returns.
                    // Zero it so a verbatim, file-format copy of the assembly doesn't linger in the
                    // managed heap until GC, where a trivial scan-for-"MZ" memory dump would find it
                    // and carve out a directly-runnable DLL. This does not prevent a determined
                    // in-memory dump (the CLR still holds a runnable copy to execute against) — it
                    // only removes the easiest, tooling-agnostic grab.
                    CryptographicOperations.ZeroMemory(assemblyBytes);
                }
            }

            return assemblies;
        }

        public IEnumerable<string> DecryptScripts(string[] encryptedScripts)
        {
            return DecryptContent(encryptedScripts).Select(decryptedScript => Encoding.UTF8.GetString(decryptedScript));
        }

        private IEnumerable<byte[]> DecryptContent(string[] content)
        {
            if (string.IsNullOrEmpty(appconfig.Id) || string.IsNullOrWhiteSpace(appconfig.SubscriptionId))
            {
                _logger.LogWarning($"{nameof(appconfig.Id)} and {nameof(appconfig.SubscriptionId)} must be provided to attempt loading remote assemblies/scripts");
                return Array.Empty<byte[]>();
            }

            var assemblies = content.Select(piece =>
            {
                var byteContent = Convert.FromBase64String(piece);
                var encryptedContent = byteContent.Take(byteContent.Length - (TagLength + NonceLength)).ToArray();
                var tag = byteContent.Skip(byteContent.Length - (TagLength + NonceLength)).Take(TagLength).ToArray();
                var nonce = byteContent.Skip(byteContent.Length - NonceLength).Take(NonceLength).ToArray();
                var decryptedContent = new byte[encryptedContent.Length];

                var keyGen = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(appconfig.SubscriptionId), Encoding.UTF8.GetBytes(appconfig.Id), IterationCount, HashAlgorithmName.SHA512);
                var encryption = new AesGcm(keyGen.GetBytes(KeyLength),TagLength);

                try
                {
                    encryption.Decrypt(nonce, encryptedContent, tag, decryptedContent);
                }

                catch (CryptographicException ex)
                {
                    _logger.LogError(ex, "Could not decrypt remote plugin assemblies");
                }

                return decryptedContent;
            });

            return assemblies.ToArray();
        }
    }
}
