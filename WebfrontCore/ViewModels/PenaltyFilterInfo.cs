using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.ViewModels
{
    public class PenaltyFilterInfo
    {
        public int Offset { get; set; }
        public SharedLibraryCore.Objects.Penalty.PenaltyType ShowOnly { get; set; }
    }
}
