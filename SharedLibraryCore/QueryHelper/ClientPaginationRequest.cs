using System.Text.Json.Serialization;
using Data.Models.Client;
using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.QueryHelper
{
    public class ClientPaginationRequest : PaginationRequest
    {
        public int ClientId { get; set; }
        [JsonIgnore]
        public bool IsPrivileged { get; set; }
        [JsonIgnore]
        public EFClient.Permission RequestPermission { get; set; }
    }
}
