using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.IO
{
    class GameLogReader : IGameLogReader
    {
        private readonly IEventParser _parser;
        private readonly string _logFile;
        private readonly ILogger _logger;

        public long Length => new FileInfo(_logFile).Length;

        public int UpdateInterval => 300;

        public GameLogReader(string logFile, IEventParser parser, ILogger<GameLogReader> logger)
        {
            _logFile = logFile;
            _parser = parser;
            _logger = logger;
        }

        public async Task<IEnumerable<GameEvent>> ReadEventsFromLog(long fileSizeDiff, long startPosition)
        {
            // allocate the bytes for the new log lines
            List<string> logLines = new List<string>();

            // open the file as a stream
            using (FileStream fs = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buff = new byte[fileSizeDiff];
                fs.Seek(startPosition, SeekOrigin.Begin);
                await fs.ReadAsync(buff, 0, (int)fileSizeDiff);
                var stringBuilder = new StringBuilder();
                char[] charBuff = Utilities.EncodingType.GetChars(buff);

                foreach (char c in charBuff)
                {
                    if (c == '\n')
                    {
                        logLines.Add(stringBuilder.ToString());
                        stringBuilder = new StringBuilder();
                    }

                    else if (c != '\r')
                    {
                        stringBuilder.Append(c);
                    }
                }

                if (stringBuilder.Length > 0)
                {
                    logLines.Add(stringBuilder.ToString());
                }
            }

            List<GameEvent> events = new List<GameEvent>();

            // parse each line
            foreach (string eventLine in logLines.Where(_line => _line.Length > 0))
            {
                try
                {
                    var gameEvent = _parser.GenerateGameEvent(eventLine);
                    events.Add(gameEvent);
                }

                catch (Exception e)
                {
                    _logger.LogError(e, "Could not properly parse event line {@eventLine}", eventLine);
                }
            }

            return events;
        }
    }
}
