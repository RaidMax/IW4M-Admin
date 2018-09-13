using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SharedLibraryCore.Objects.Player;

namespace SharedLibraryCore.Dtos
{
    public class ClientInfo
    {
        public string Name { get; set; }
        public int ClientId { get; set; }
        public int LinkId { get; set; }
        public Permission Level { get; set; }
    }
}
