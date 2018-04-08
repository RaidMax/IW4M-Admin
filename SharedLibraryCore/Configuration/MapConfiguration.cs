using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Configuration
{
    public class MapConfiguration
    {
        public Game Game { get; set; }
        public List<Map> Maps { get; set; }
    }
}
