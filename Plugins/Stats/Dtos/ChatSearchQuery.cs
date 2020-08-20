using SharedLibraryCore.QueryHelper;
using System;

namespace Stats.Dtos
{
    public class ChatSearchQuery : ClientPaginationRequest
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
        public new int? ClientId { get; set; }

        /// <summary>
        /// only look for messages sent after this date
        /// </summary>
        public DateTime SentAfter { get; set; } = DateTime.UtcNow.AddYears(-100);

        /// <summary>
        /// only look for messages sent before this date0
        /// </summary>
        public DateTime SentBefore { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// indicates if the chat is on the meta page
        /// </summary>
        public bool IsProfileMeta { get; set; }
    }
}
