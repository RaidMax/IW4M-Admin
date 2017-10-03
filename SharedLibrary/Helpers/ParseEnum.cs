using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Helpers
{
    public class ParseEnum<T>
    {
        public static T Get(string e, Type type)
        {
            try
            {
                return (T)Enum.Parse(type, e);
            }

            catch (Exception)
            {
                return (T)(Enum.GetValues(type).GetValue(0));
            }
        }
    }
}
