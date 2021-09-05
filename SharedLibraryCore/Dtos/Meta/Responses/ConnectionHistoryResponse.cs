using Data.Models;

namespace SharedLibraryCore.Dtos.Meta.Responses
{
    public class ConnectionHistoryResponse : BaseMetaResponse
    {
        public string ServerName { get; set; }
        public Reference.ConnectionType ConnectionType { get; set; }
    }
}