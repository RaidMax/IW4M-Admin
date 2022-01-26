using System.Linq;

namespace SharedLibraryCore.Dtos.Meta.Responses
{
    public class MessageResponse : BaseMetaResponse
    {
        public long ServerId { get; set; }
        public string Message { get; set; }
        public bool IsHidden { get; set; }

        /// <summary>
        ///     name of the client
        /// </summary>
        public string ClientName { get; set; }

        /// <summary>
        ///     hostname of the server
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        ///     specifies the game the chat occured on
        /// </summary>
        public Server.Game GameName { get; set; }

        /// <summary>
        ///     indicates if the chat message is a quick message phrase
        /// </summary>
        public bool IsQuickMessage { get; set; }

        /// <summary>
        ///     indicates if the message was sent ingame
        /// </summary>
        public bool SentIngame { get; set; }

        public string HiddenMessage => string.Concat(Enumerable.Repeat('●', Message.Length));
    }
}