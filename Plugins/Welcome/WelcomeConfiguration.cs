using SharedLibrary.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Welcome_Plugin
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
