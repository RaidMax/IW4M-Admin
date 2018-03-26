using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary.Database.Models;
using System.ComponentModel.DataAnnotations.Schema;
using SharedLibrary.Helpers;
using System.ComponentModel.DataAnnotations;

namespace StatsPlugin.Models
{
    public class EFClientKill : SharedEntity
    {
        public EFClientKill() { }

        [Key]
        public long KillId { get; set; }
        public int VictimId { get; set; }
        [ForeignKey("VictimId")]
        public virtual EFClient Victim { get; set; }
        public int AttackerId { get; set; }
        [ForeignKey("AttackerId")]
        public virtual EFClient Attacker { get; set; }
        public int ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual EFServer Server { get; set; }
        public IW4Info.HitLocation HitLoc { get; set; }
        public IW4Info.MeansOfDeath DeathType { get; set; }
        public int Damage { get; set; }
        public IW4Info.WeaponName Weapon { get; set; }
        public Vector3 KillOrigin { get; set; }
        public Vector3 DeathOrigin { get; set; }
        public Vector3 ViewAngles { get; set; }
        public DateTime When { get; set; }
        // http://wiki.modsrepository.com/index.php?title=Call_of_Duty_5:_Gameplay_standards for conversion to meters
        [NotMapped]
        public double Distance => Vector3.Distance(KillOrigin, DeathOrigin) * 0.0254;
        public IW4Info.MapName Map { get; set; }
        [NotMapped]
        public long TimeOffset { get; set; }
        [NotMapped]
        public bool IsKillstreakKill { get; set; }
        [NotMapped]
        public float AdsPercent { get; set; }
    }
}
