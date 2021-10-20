using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.RConParsers
{
    /// <inheritdoc cref="IStatusResponse"/>
    public class StatusResponse : IStatusResponse
    {
        public string Map { get; set; }
        public string GameType { get; set; }
        public string Hostname { get; set; }
        public int? MaxClients { get; set; }
        public EFClient[] Clients { get; set; }
    }
}