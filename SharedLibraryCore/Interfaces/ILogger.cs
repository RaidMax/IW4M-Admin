using System;

namespace SharedLibraryCore.Interfaces
{
    [Obsolete]
    public interface ILogger
    {
        void WriteVerbose(string msg);
        void WriteInfo(string msg);
        void WriteDebug(string msg);
        void WriteWarning(string msg);
        void WriteError(string msg);
        void WriteAssert(bool condition, string msg);
    }
}