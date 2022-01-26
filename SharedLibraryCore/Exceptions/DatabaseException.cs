using System;

namespace SharedLibraryCore.Exceptions
{
    public class DatabaseException : Exception
    {
        public DatabaseException(string msg) : base(msg)
        {
        }
    }
}