using System;

namespace SharedLibraryCore.Exceptions
{
    public class SerializeException : Exception
    {
        public SerializeException(string msg) : base(msg)
        {
        }
    }
}