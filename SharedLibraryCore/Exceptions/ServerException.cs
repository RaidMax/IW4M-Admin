using System;

namespace SharedLibraryCore.Exceptions
{
    public class ServerException : Exception
    {
        public ServerException(string msg) : base(msg)
        {
        }
    }
}