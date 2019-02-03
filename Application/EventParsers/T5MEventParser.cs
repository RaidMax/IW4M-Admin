using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application.EventParsers
{
    class T5MEventParser : BaseEventParser
    {
        public T5MEventParser() : base()
        {
            Configuration.GameDirectory = "v2";
        }
    }
}
