using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.EventParsers
{
    class IW5EventParser : IW4EventParser
    {
        public override string GetGameDir() => "rzodemo";
    }
}
