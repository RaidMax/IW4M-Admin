using System;

namespace SharedLibraryCore.RCon
{
    public static class StaticHelpers
    {
        public enum QueryType
        {
            GET_STATUS,
            GET_INFO,
            DVAR,
            COMMAND,
        }

        public static char SeperatorChar = (char)int.Parse("0a", System.Globalization.NumberStyles.AllowHexSpecifier);
        public static readonly TimeSpan SocketTimeout = new TimeSpan(0, 0, 2);
    }
}
