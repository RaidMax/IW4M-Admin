using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.RConParsers
{
    /// <inheritdoc cref="IStatusResponse"/>
    public class StatusResponse : IStatusResponse
    {
        public string Map { get; init; }
        public string GameType { get; init; }
        public string Hostname { get; init; }
        public int? MaxClients { get; init; }
        public EFClient[] Clients { get; init; }
        public string[] RawResponse { get; set; }
    }
}
