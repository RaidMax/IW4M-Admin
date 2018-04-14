using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Exceptions
{
    public class AuthorizationException : Exception
    {
        public AuthorizationException(string message) : base (message) { }
    }
}
