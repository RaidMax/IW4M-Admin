using System.Collections.Generic;
using System.Reflection;

namespace SharedLibraryCore.Interfaces
{
    public interface IRemoteAssemblyHandler
    {
        IEnumerable<Assembly> DecryptAssemblies(string[] encryptedAssemblies);
        IEnumerable<string> DecryptScripts(string[] encryptedScripts);
    }
}
