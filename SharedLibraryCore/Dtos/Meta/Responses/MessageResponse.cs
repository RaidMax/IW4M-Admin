using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Dtos.Meta.Responses
{
    public class MessageResponse : BaseMetaResponse
    {
        public long ServerId { get; set; }
        public string Message { get; set; }
    }
}
