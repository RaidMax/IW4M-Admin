using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Exceptions
{
    public class CommandException : ServerException
    {
        public CommandException(string msg) : base(msg) { }
        // .data contains
        // "command_name"
    }
}
