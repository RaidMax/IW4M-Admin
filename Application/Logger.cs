using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace IW4MAdmin.Application
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

        readonly string FileName;
        readonly SemaphoreSlim OnLogWriting;

        public Logger(string fn)
        {
            FileName = Path.Join("Log", $"{fn}-{DateTime.Now.ToString("yyyyMMddHHmmssffff")}.log");
            OnLogWriting = new SemaphoreSlim(1,1);
        }

        void Write(string msg, LogType type)
        {
            OnLogWriting.Wait();

            string stringType = type.ToString();

            try
            {
                stringType = Utilities.CurrentLocalization.LocalizationIndex[$"GLOBAL_{type.ToString().ToUpper()}"];
            }

            catch (Exception) { }

            string LogLine = $"[{DateTime.Now.ToString("MM.dd.yyy HH:mm:ss.fff")}] - {stringType}: {msg}";
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

            OnLogWriting.Release(1);
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
