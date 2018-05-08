using SharedLibraryCore;
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
            UserAnnouncementMessage = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_WELCOME_USERANNOUNCE"];
            UserWelcomeMessage = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_WELCOME_USERWELCOME"];
            PrivilegedAnnouncementMessage = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_WELCOME_PRIVANNOUNCE"];
            return this;
        }

        public string Name() => "WelcomeConfiguration";
    }
}
