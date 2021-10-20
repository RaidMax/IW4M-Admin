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
        private const int keyLength = 32;
        private const int tagLength = 16;
        private const int nonceLength = 12;
        private const int iterationCount = 10000;

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
                .Select(decryptedAssembly => Assembly.Load(decryptedAssembly));
        }

        public IEnumerable<string> DecryptScripts(string[] encryptedScripts)
        {
            return DecryptContent(encryptedScripts).Select(decryptedScript => Encoding.UTF8.GetString(decryptedScript));
        }

        private byte[][] DecryptContent(string[] content)
        {
            if (string.IsNullOrEmpty(_appconfig.Id) || string.IsNullOrWhiteSpace(_appconfig.SubscriptionId))
            {
                _logger.LogWarning($"{nameof(_appconfig.Id)} and {nameof(_appconfig.SubscriptionId)} must be provided to attempt loading remote assemblies/scripts");
                return new byte[0][];
            }

            var assemblies = content.Select(piece =>
            {
                byte[] byteContent = Convert.FromBase64String(piece);
                byte[] encryptedContent = byteContent.Take(byteContent.Length - (tagLength + nonceLength)).ToArray();
                byte[] tag = byteContent.Skip(byteContent.Length - (tagLength + nonceLength)).Take(tagLength).ToArray();
                byte[] nonce = byteContent.Skip(byteContent.Length - nonceLength).Take(nonceLength).ToArray();
                byte[] decryptedContent = new byte[encryptedContent.Length];

                var keyGen = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(_appconfig.SubscriptionId), Encoding.UTF8.GetBytes(_appconfig.Id.ToString()), iterationCount, HashAlgorithmName.SHA512);
                var encryption = new AesGcm(keyGen.GetBytes(keyLength));

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
