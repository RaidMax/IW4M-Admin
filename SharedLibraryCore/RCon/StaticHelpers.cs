using System;

namespace SharedLibraryCore.RCon
{
    public static class StaticHelpers
    {
        /// <summary>
        /// defines the type of RCon query sent to a server
        /// </summary>
        public enum QueryType
        {
            /// <summary>
            /// retrieve the status of a server
            /// does not require RCon password
            /// </summary>
            GET_STATUS,
            /// <summary>
            /// retrieve the information of a server
            /// server responds with key/value pairs
            /// RCon password is required
            /// </summary>
            GET_INFO,
            /// <summary>
            /// retrieve the value of a DVAR
            /// RCon password is required
            /// </summary>
            DVAR,
            /// <summary>
            /// execute a command
            /// RCon password is required
            /// </summary>
            COMMAND,
        }

        /// <summary>
        /// line seperator char included in response from the server
        /// </summary>
        public static char SeperatorChar = (char)int.Parse("0a", System.Globalization.NumberStyles.AllowHexSpecifier);
        /// <summary>
        /// timeout in seconds to wait for a socket send or receive before giving up
        /// </summary>
        public static readonly int SocketTimeout = 10000;
        /// <summary>
        /// interval in milliseconds to wait before sending the next RCon request
        /// </summary>
        public static readonly int FloodProtectionInterval = 635;
        public static readonly int AllowedConnectionFails = 3;
    }
}
