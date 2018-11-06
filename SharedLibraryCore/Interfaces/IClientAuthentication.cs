using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    public interface IClientAuthentication
    {
        /// <summary>
        /// request authentication when a client join event
        /// occurs in the log, as no IP is given
        /// </summary>
        /// <param name="client">client that has joined from the log</param>
        void RequestClientAuthentication(EFClient client);
        /// <summary>
        /// get all clients that have been authenticated by the status poll
        /// </summary>
        /// <returns>list of all authenticated clients</returns>
        IList<EFClient> GetAuthenticatedClients();
        /// <summary>
        /// authenticate a list of clients from status poll
        /// </summary>
        /// <param name="clients">list of clients to authenticate</param>
        void AuthenticateClients(IList<EFClient> clients);
    }
}
