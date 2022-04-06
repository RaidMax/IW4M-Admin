using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Login
{
    class Configuration : IBaseConfiguration
    {
        public bool RequirePrivilegedClientLogin { get; set; }

        public IBaseConfiguration Generate()
        {
            return this;
        }

        public string Name() => "LoginConfiguration";
    }
}
