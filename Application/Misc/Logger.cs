using System;
using Microsoft.Extensions.Logging;
using ILogger = SharedLibraryCore.Interfaces.ILogger;

namespace IW4MAdmin.Application
{
    [Obsolete]
    public class Logger : ILogger
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public Logger(ILogger<Logger> logger)
        {
            _logger = logger;
        }

        public void WriteVerbose(string msg)
        {
            _logger.LogInformation(msg);
        }

        public void WriteDebug(string msg)
        {
            _logger.LogDebug(msg);
        }

        public void WriteError(string msg)
        {
            _logger.LogError(msg);
        }

        public void WriteInfo(string msg)
        {
            WriteVerbose(msg);
        }

        public void WriteWarning(string msg)
        {
            _logger.LogWarning(msg);
        }

        public void WriteAssert(bool condition, string msg)
        {
            throw new NotImplementedException();
        }
    }
}
