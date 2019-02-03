using System.IO;

namespace IW4MAdmin.Application.EventParsers
{
    class T6MEventParser : BaseEventParser
    {
        public T6MEventParser() : base()
        {
            Configuration.GameDirectory = $"t6r{Path.DirectorySeparatorChar}data";
        }
    }
}
