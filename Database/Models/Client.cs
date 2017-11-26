using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database.Models
{
    public class Client
    {
        [Key]
        public int ClientId { get; set; }

        [Required]
        public string Name { get; set; }
        [Required]
        public string NetworkId { get; set; }
        [Required]
        public SharedLibrary.Player.Permission Level { get; set; }
        public int Connections { get; set; }
        [Required]
        public string IPAddress { get; set; }
        [Required]
        public DateTime LastConnection { get; set; }
         public bool Masked { get; set; }

        public SharedLibrary.Player ToPlayer()
        {
            return new SharedLibrary.Player()
            {
                Name = Name,
               Connections = Connections,
               DatabaseID = ClientId,
               NetworkID = NetworkId,
               Level = Level,
               IP = IPAddress,
               LastConnection = LastConnection,
               Masked = Masked
            };
        }
    }
}
