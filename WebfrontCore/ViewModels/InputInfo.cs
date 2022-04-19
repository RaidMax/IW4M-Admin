using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.ViewModels
{
    public class InputInfo
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Placeholder { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public Dictionary<string, string> Values { get; set; } 
        public bool Checked { get; set; }
        public bool Required { get; set; }
    }
}
