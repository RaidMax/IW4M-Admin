using System;
using System.Collections.Generic;
using System.Text;
using static SharedLibraryCore.Objects.Player;

namespace SharedLibraryCore.Objects
{
    public sealed class ClientPermission
    {
        public Permission Level { get; set; }
        public string Name { get; set; }
    }
}
