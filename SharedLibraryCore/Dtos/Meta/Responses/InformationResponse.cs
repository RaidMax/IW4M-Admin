using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Dtos.Meta.Responses
{
    public class InformationResponse : BaseMetaResponse
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string ToolTipText { get; set; }
    }
}
