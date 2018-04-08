using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Exceptions
{
    public class NetworkException : ServerException
    {
        public NetworkException(string msg) : base(msg) { }
    }
}
