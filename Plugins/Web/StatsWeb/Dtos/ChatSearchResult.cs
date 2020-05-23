using System;

namespace StatsWeb.Dtos
{
    public class ChatSearchResult
    {
        /// <summary>
        /// name of the client
        /// </summary>
        public string ClientName { get; set; }

        /// <summary>
        /// client id
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        /// hostname of the server
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        /// chat message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// date the chat occured on
        /// </summary>
        public DateTime Date { get; set; }
    }
}
