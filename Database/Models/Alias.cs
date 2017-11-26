using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database.Models
{
    public class Alias
    {
        [Key]
        public int AliasId { get; set; }
        public int ClientId { get; set; }
        public string Name { get; set; }
        public string IP { get; set; }
    }
}
