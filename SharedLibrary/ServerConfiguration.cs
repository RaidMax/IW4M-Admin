using SharedLibrary.Interfaces;

namespace SharedLibrary
{
    public class ServerConfiguration : Serialize<ServerConfiguration>
    {
        public string IP;
        public int Port;
        public string Password;
        public string FtpPrefix;
        public bool AllowMultipleOwners;
        public bool AllowTrustedRank;
        public string RestartUsername;
        public string RestartPassword;
        public bool EnableAntiCheat;
        public bool AllowClientVpn;

        public override string Filename()
        {
            return $"{Utilities.OperatingDirectory}config/servers/{IP}_{Port}.cfg";
        }
    }
}
