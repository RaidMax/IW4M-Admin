using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dtos
{
    public class ProfileMeta : SharedInfo
    {
        public DateTime When { get; set; }
        public string WhenString => Utilities.GetTimePassed(When, false);
        public string Key { get; set; }
        public dynamic Value { get; set; }
        public virtual string Class => Value.GetType().ToString();
    }
}
