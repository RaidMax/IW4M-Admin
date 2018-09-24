using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IW4MAdmin.Plugins.Stats.Models
{
    /// <summary>
    /// This class houses the information for anticheat snapshots (used for validating a ban)
    /// </summary>
    public class EFACSnapshot : SharedEntity
    {
        [Key]
        public int SnapshotId { get; set; }
        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public EFClient Client { get; set; }

        public DateTime When { get; set; }
        public int CurrentSessionLength { get; set; }
        public int TimeSinceLastEvent { get; set; }
        public double EloRating { get; set; }
        public int SessionScore { get; set; }
        public double SessionSPM { get; set; }
        public int Hits { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public double CurrentStrain { get; set; }
        public double StrainAngleBetween { get; set; }
        public double SessionAngleOffset { get; set; }
        public int LastStrainAngleId { get; set; }
        [ForeignKey("LastStrainAngleId")]
        public Vector3 LastStrainAngle { get; set; }
        public int HitOriginId { get; set; }
        [ForeignKey("HitOriginId")]
        public Vector3 HitOrigin { get; set; }
        public int HitDestinationId { get; set; }
        [ForeignKey("HitDestinationId")]
        public Vector3 HitDestination { get; set; }
        public double Distance { get; set; }
        public int CurrentViewAngleId { get; set; }
        [ForeignKey("CurrentViewAngleId")]
        public Vector3 CurrentViewAngle { get; set; }
        public IW4Info.WeaponName WeaponId { get; set; }
        public IW4Info.HitLocation HitLocation { get; set; }
        public IW4Info.MeansOfDeath HitType { get; set; }
        public virtual ICollection<Vector3> PredictedViewAngles { get; set; }
    }
}
