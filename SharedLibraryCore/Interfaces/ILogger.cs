using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface ILogger
    {
        void WriteVerbose(string msg);
        void WriteInfo(string msg);
        void WriteDebug(string msg);
        void WriteWarning(string msg);
        void WriteError(string msg);
    }
}
