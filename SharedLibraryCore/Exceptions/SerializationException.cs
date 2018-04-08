using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Exceptions
{
    public class SerializeException : Exception
    {
        public SerializeException(string msg) : base(msg) { }
    }
}
