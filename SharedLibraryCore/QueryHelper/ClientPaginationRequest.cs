using SharedLibraryCore.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.QueryHelper
{
    public class ClientPaginationRequest : PaginationRequest
    {
        public int ClientId { get; set; }
    }
}
