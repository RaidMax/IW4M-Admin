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

        public override string Filename()
        {
            return $"config/servers/{IP}_{Port}.cfg";
        }
    }
}
