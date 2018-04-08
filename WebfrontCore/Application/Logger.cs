using System;
using System.IO;

namespace IW4MAdmin
{
    class Logger : SharedLibraryCore.Interfaces.ILogger
    {
        enum LogType
        { 
            Verbose,
            Info,
            Debug,
            Warning,
            Error,
            Assert
        }

        string FileName;
        object ThreadLock;

        public Logger(string fn)
        {
            FileName = fn;
            ThreadLock = new object();
            if (File.Exists(fn))
                File.Delete(fn);
        }

        void Write(string msg, LogType type)
        {
            string LogLine = $"[{DateTime.Now.ToString("HH:mm:ss")}] - {type}: {msg}";
            lock (ThreadLock)
            {
#if DEBUG
            // lets keep it simple and dispose of everything quickly as logging wont be that much (relatively)

            Console.WriteLine(LogLine);
            File.AppendAllText(FileName, LogLine + Environment.NewLine);
#else
                if (type == LogType.Error || type == LogType.Verbose)
                    Console.WriteLine(LogLine);
                //if (type != LogType.Debug)
                File.AppendAllText(FileName, $"{LogLine}{Environment.NewLine}");
#endif
            }
        }

        public void WriteVerbose(string msg)
        {
            Write(msg, LogType.Verbose);
        }

        public void WriteDebug(string msg)
        {
            Write(msg, LogType.Debug);
        }

        public void WriteError(string msg)
        {
            Write(msg, LogType.Error);
        }

        public void WriteInfo(string msg)
        {
            Write(msg, LogType.Info);
        }

        public void WriteWarning(string msg)
        {
            Write(msg, LogType.Warning);
        }

        public void WriteAssert(bool condition, string msg)
        {
            if (!condition)
                Write(msg, LogType.Assert);
        }
    }
}
