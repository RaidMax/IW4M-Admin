using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application.EventParsers
{
    class IW3EventParser : IW4EventParser
    {
        public IW3EventParser() : base()
        {
            Configuration.GameDirectory = "main";
        }
    }
}
