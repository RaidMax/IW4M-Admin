using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Helpers
{
    public class Configuration<T> : Interfaces.Serialize<T>
    {
        string FilePostfix;
        public Configuration(Server S)
        {
            FilePostfix = $"_{S.GetIP()}_{S.GetPort()}.cfg";
        }

        public T Read()
        {
            try
            {
                return Read();
            }

            catch (Exceptions.SerializeException)
            {
                return default(T);
            }
        }

        public bool Write(T Data)
        {
            try
            {
                Write(Filename(), Data);
                return true;
            }
            
            catch(Exceptions.SerializeException)
            {
                return false;
            }
        }

        public override string Filename()
        {
            return $"config/{typeof(T).ToString()}_{FilePostfix}"; 
        }

    }
}
