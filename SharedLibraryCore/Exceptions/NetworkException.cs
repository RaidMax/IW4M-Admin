using System.Net.Sockets;

namespace SharedLibraryCore.Exceptions
{
    public class NetworkException : ServerException
    {
        public NetworkException(string msg) : base(msg)
        {
        }

        public NetworkException(string msg, Socket s) : base(msg)
        {
            Data.Add("socket", s);
        }
    }
}