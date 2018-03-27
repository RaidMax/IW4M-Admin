using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.ViewModels
{
    public class ActionInfo
    {
        public string Name { get; set; }
        public List<InputInfo> Inputs { get; set; }
        public string ActionButtonLabel { get; set; }
        public string Action { get; set; }
    }
}
