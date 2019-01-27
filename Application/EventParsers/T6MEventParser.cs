using System.IO;

namespace IW4MAdmin.Application.EventParsers
{
    class T6MEventParser : IW4EventParser
    {
        public T6MEventParser() : base()
        {
            Configuration.GameDirectory = $"t6r{Path.DirectorySeparatorChar}data";
        }
    }
}
