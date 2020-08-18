using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Dtos.Meta.Requests
{
    public class BaseClientMetaRequest : PaginationRequest
    {
        public int ClientId { get; set; }
    }
}
