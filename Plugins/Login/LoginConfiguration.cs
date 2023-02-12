using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Login
{
    public class LoginConfiguration : IBaseConfiguration
    {
        public bool RequirePrivilegedClientLogin { get; set; }

        public IBaseConfiguration Generate()
        {
            return this;
        }

        public string Name() => "LoginConfiguration";
    }
}
