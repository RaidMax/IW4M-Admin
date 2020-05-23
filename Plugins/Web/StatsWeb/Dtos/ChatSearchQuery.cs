using SharedLibraryCore.Dtos;
using System;

namespace StatsWeb.Dtos
{
    public class ChatSearchQuery : PaginationInfo
    {
        /// <summary>
        /// specifies the partial content of the message to search for
        /// </summary>
        public string MessageContains { get; set; }

        /// <summary>
        /// identifier for the server
        /// </summary>
        public string ServerId { get; set; }

        /// <summary>
        /// identifier for the client
        /// </summary>
        public int? ClientId { get; set; }

        /// <summary>
        /// only look for messages sent after this date
        /// </summary>
        public DateTime SentAfter { get; set; } = DateTime.UtcNow.AddYears(-100);

        /// <summary>
        /// only look for messages sent before this date0
        /// </summary>
        public DateTime SentBefore { get; set; } = DateTime.UtcNow;
    }
}
