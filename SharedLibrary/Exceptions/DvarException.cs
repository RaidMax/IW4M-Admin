using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Exceptions
{
    public class DvarException : ServerException
    {
        public DvarException(string msg) : base(msg) { }
    }
}
