using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;

namespace IW4MAdmin.Application.EventParsers
{
    class T6MEventParser : IW4EventParser
    {
        public override string GetGameDir() => $"t6r{Path.DirectorySeparatorChar}data";
    }
}
