using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Dtos
{
    public class IW4MAdminInfo
    {
        public int TotalClientCount { get; set; }
        public int RecentClientCount { get; set; }
        public int TotalOccupiedClientSlots { get; set; }
        public int TotalAvailableClientSlots { get; set; }
    }
}
