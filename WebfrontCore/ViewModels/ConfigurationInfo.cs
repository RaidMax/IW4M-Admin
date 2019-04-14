using SharedLibraryCore.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace WebfrontCore.ViewModels
{
    public class ConfigurationInfo
    {
        public string PropertyName { get; set; }
        public PropertyInfo PropertyInfo { get; set; }
        public IList PropertyValue { get; set; }
        public IBaseConfiguration Configuration { get; set; }
        public int NewItemCount { get; set; }
    }
}
