using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;

namespace WebfrontCore.Controllers.API.Models;

public class ClientMetaRequest : ClientPaginationRequest
{
    public long? StartAt { get; set; }
    public MetaType? MetaType { get; set; }
}
