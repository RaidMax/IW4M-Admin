using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Exceptions
{
    public class ServerException : Exception
    {
        public ServerException(string msg) : base(msg) { }
    }
}
