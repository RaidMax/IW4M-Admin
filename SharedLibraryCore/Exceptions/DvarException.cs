using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Exceptions
{
    public class DvarException : ServerException
    {
        public DvarException(string msg) : base(msg) { }
    }
}
