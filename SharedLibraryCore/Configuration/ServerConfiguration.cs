using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Configuration
{
    public class ServerConfiguration : IBaseConfiguration
    {
        public string IPAddress { get; set; }
        public ushort Port { get; set; }
        public string Password { get; set; }
        public IList<string> Rules { get; set; }
        public IList<string> AutoMessages { get; set; }
        public bool UseT6MParser { get; set; }
        public string ManualLogPath { get; set; }
        public int ReservedSlotNumber { get; set; }

        public IBaseConfiguration Generate()
        {
            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            while (string.IsNullOrEmpty(IPAddress))
            {
                string input = Utilities.PromptString(loc["SETUP_SERVER_IP"]);

                if (System.Net.IPAddress.TryParse(input, out System.Net.IPAddress ip))
                    IPAddress = input;
            }

            while(Port < 1)
            {
                string input = Utilities.PromptString(loc["SETUP_SERVER_PORT"]);
                if (UInt16.TryParse(input, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out ushort port))
                    Port = port;
            }

            Password = Utilities.PromptString(loc["SETUP_SERVER_RCON"]);
            AutoMessages = new List<string>();
            Rules = new List<string>();
            UseT6MParser = Utilities.PromptBool(loc["SETUP_SERVER_USET6M"]);
            ReservedSlotNumber = loc["SETUP_SERVER_RESERVEDSLOT"].PromptInt(null, 0, 32);

            return this;
        }

        public string Name() => "ServerConfiguration";
    }
}
