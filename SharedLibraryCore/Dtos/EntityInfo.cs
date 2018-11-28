using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Dtos
{
    /// <summary>
    /// This class holds the basic info for api entities
    /// </summary>
    public class EntityInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }
}
