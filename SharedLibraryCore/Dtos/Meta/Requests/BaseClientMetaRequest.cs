namespace SharedLibraryCore.Dtos.Meta.Requests
{
    public class BaseClientMetaRequest : PaginationRequest
    {
        public int ClientId { get; set; }
    }
}