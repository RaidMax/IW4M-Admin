using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Welcome
{
    class WelcomeConfiguration : IBaseConfiguration
    {
        public string UserAnnouncementMessage { get; set; }
        public string UserWelcomeMessage { get; set; }
        public string PrivilegedAnnouncementMessage { get; set; }

        public IBaseConfiguration Generate()
        {
            UserAnnouncementMessage = "^5{{ClientName}} ^7hails from ^5{{ClientLocation}}";
            UserWelcomeMessage = "Welcome ^5{{ClientName}}^7, this is your ^5{{TimesConnected}} ^7time connecting!";
            PrivilegedAnnouncementMessage = "{{ClientLevel}} {{ClientName}} has joined the server";
            return this;
        }

        public string Name() => "WelcomeConfiguration";
    }
}
