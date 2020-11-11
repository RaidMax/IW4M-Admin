using System;

namespace SharedLibraryCore.Exceptions
{
    public class RConException : Exception
    {
        public RConException(string message) : base(message)
        {
        }
    }
}