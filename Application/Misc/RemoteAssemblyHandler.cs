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
    public class RemoteAssemblyHandler : IRemoteAssemblyHandler
    {
        private const int KeyLength = 32;
        private const int TagLength = 16;
        private const int NonceLength = 12;
        private const int IterationCount = 10000;

        private readonly ApplicationConfiguration _appconfig;
        private readonly ILogger _logger;

        public RemoteAssemblyHandler(ILogger<RemoteAssemblyHandler> logger, ApplicationConfiguration appconfig)
        {
            _appconfig = appconfig;
            _logger = logger;
        }

        public IEnumerable<Assembly> DecryptAssemblies(string[] encryptedAssemblies)
        {
            return DecryptContent(encryptedAssemblies)
                .Select(Assembly.Load);
        }

        public IEnumerable<string> DecryptScripts(string[] encryptedScripts)
        {
            return DecryptContent(encryptedScripts).Select(decryptedScript => Encoding.UTF8.GetString(decryptedScript));
        }

        private IEnumerable<byte[]> DecryptContent(string[] content)
        {
            if (string.IsNullOrEmpty(_appconfig.Id) || string.IsNullOrWhiteSpace(_appconfig.SubscriptionId))
            {
                _logger.LogWarning($"{nameof(_appconfig.Id)} and {nameof(_appconfig.SubscriptionId)} must be provided to attempt loading remote assemblies/scripts");
                return Array.Empty<byte[]>();
            }

            var assemblies = content.Select(piece =>
            {
                var byteContent = Convert.FromBase64String(piece);
                var encryptedContent = byteContent.Take(byteContent.Length - (TagLength + NonceLength)).ToArray();
                var tag = byteContent.Skip(byteContent.Length - (TagLength + NonceLength)).Take(TagLength).ToArray();
                var nonce = byteContent.Skip(byteContent.Length - NonceLength).Take(NonceLength).ToArray();
                var decryptedContent = new byte[encryptedContent.Length];

                var keyGen = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(_appconfig.SubscriptionId), Encoding.UTF8.GetBytes(_appconfig.Id), IterationCount, HashAlgorithmName.SHA512);
                var encryption = new AesGcm(keyGen.GetBytes(KeyLength));

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
