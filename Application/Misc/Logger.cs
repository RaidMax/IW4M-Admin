﻿using IW4MAdmin.Application.IO;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace IW4MAdmin.Application
{
    public class Logger : ILogger
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
        readonly ReaderWriterLockSlim WritingLock;
        static readonly short MAX_LOG_FILES = 10;

        public Logger(string fn)
        {
            FileName = Path.Join(Utilities.OperatingDirectory, "Log", $"{fn}.log");
            WritingLock = new ReaderWriterLockSlim();
            RotateLogs();
        }

        ~Logger()
        {
            WritingLock.Dispose();
        }

        /// <summary>
        /// rotates logs when log is initialized
        /// </summary>
        private void RotateLogs()
        {
            string maxLog = FileName + MAX_LOG_FILES;

            if (File.Exists(maxLog))
            {
                File.Delete(maxLog);
            }

            for (int i = MAX_LOG_FILES - 1; i >= 0; i--)
            {
                string logToMove = i == 0 ? FileName : FileName + i;
                string movedLogName = FileName + (i + 1);

                if (File.Exists(logToMove))
                {
                    File.Move(logToMove, movedLogName);
                }
            }
        }

        void Write(string msg, LogType type)
        {
            WritingLock.EnterWriteLock();

            string stringType = type.ToString();
            msg = msg.StripColors();

            try
            {
                stringType = Utilities.CurrentLocalization.LocalizationIndex[$"GLOBAL_{type.ToString().ToUpper()}"];
            }

            catch (Exception) { }

            string LogLine = $"[{DateTime.Now.ToString("MM.dd.yyy HH:mm:ss.fff")}] - {stringType}: {msg}";
            try
            {
#if DEBUG
                // lets keep it simple and dispose of everything quickly as logging wont be that much (relatively)
                Console.WriteLine(msg);
#else
                if (type == LogType.Error || type == LogType.Verbose)
                {
                    Console.WriteLine(LogLine);
                }
                File.AppendAllText(FileName, $"{LogLine}{Environment.NewLine}");
#endif
            }

            catch (Exception ex)
            {
                Console.WriteLine("Well.. It looks like your machine can't event write to the log file. That's something else...");
                Console.WriteLine(ex.GetExceptionInfo());
            }

            WritingLock.ExitWriteLock();
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
