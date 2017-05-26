using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SharedLibrary.Interfaces
{
    interface ISerializable<T>
    {
        void Write();
    }

    public class SerializeException : Exception
    {
       public SerializeException(string msg) : base(msg) { }
    }

    public class Serialize<T> : ISerializable<T>
    {
        public static T Read(string filename)
        {
            try
            { 
                string configText = File.ReadAllText(filename);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(configText);
            }

            catch (Exception e)
            {
                throw new SerializeException($"Could not desialize file {filename}: {e.Message}");
            }
        }

        public void Write()
        {
            try
            {
                string configText = Newtonsoft.Json.JsonConvert.SerializeObject(this);
                File.WriteAllText(Filename(), configText);
            }

            catch (Exception e)
            {
                throw new SerializeException($"Could not serialize file {Filename()}: {e.Message}");
            }
        }

        public virtual string Filename() { return this.ToString();  }
    }
}
