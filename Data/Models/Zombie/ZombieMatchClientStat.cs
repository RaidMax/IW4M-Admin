using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models.Zombie;

public class ZombieMatchClientStat : ZombieClientStat
{
    [NotMapped] public int? JoinedRound { get; set; }
}
