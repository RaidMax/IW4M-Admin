using System.Collections.Generic;
using System.Reflection;

namespace SharedLibraryCore.Interfaces
{
    public interface IRemoteAssemblyHandler
    {
        IEnumerable<Assembly> DecryptAssemblies(string[] encryptedAssemblies);
        IEnumerable<string> DecryptScripts(string[] encryptedScripts);

        /// <summary>
        /// Decrypts remote content to its raw plaintext bytes (e.g. plugin bundle zips). Unlike
        /// <see cref="DecryptAssemblies"/>, the plaintext is returned to the caller (to hand to the bundle
        /// loader) rather than loaded here, so the caller is responsible for zeroing each buffer once the
        /// loader has consumed it.
        /// </summary>
        IEnumerable<byte[]> DecryptContent(string[] content);
    }
}