using System.Collections.Generic;

namespace SharedLibrary.Configuration
{
    public class ApplicationConfiguration
    {
        public bool EnableMultipleOwners { get; set; }
        public bool EnableTrustedRank { get; set; }
        public bool EnableClientVPNs { get; set; }
        public bool EnableAntiCheat { get; set; }
        public bool EnableDiscordLink { get; set; }
        public string DiscordInviteCode { get; set; }
        public string IPHubAPIKey { get; set; }
        public List<ServerConfiguration> Servers { get; set; }

    }
}
