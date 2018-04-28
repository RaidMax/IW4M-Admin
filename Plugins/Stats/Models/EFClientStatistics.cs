using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibraryCore.Database.Models;

namespace IW4MAdmin.Plugins.Stats.Models
{
    public class EFClientStatistics : SharedEntity
    {
        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual EFClient Client { get; set; }
        public int ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual EFServer Server { get; set; }
        [Required]
        public int Kills { get; set; }
        [Required]
        public int Deaths { get; set; }

        public virtual ICollection<EFHitLocationCount> HitLocations { get; set; }

        [NotMapped]
        public double KDR
        {
            get => Deaths == 0 ? Kills : Math.Round(Kills / (double)Deaths, 2);
        }
        [Required]
        public double SPM { get; set; }
        [Required]
        public double Skill { get; set; }
        [Required]
        public int TimePlayed { get; set; }

        [NotMapped]
        public float AverageHitOffset
        {
            get => (float)Math.Round(HitLocations.Sum(c => c.HitOffsetAverage) / Math.Max(1, HitLocations.Where(c => c.HitOffsetAverage > 0).Count()), 4);
        }
        [NotMapped]
        public int SessionKills { get; set; }
        [NotMapped]
        public int SessionDeaths { get; set; }
        [NotMapped]
        public int KillStreak { get; set; }
        [NotMapped]
        public int DeathStreak { get; set; }
        [NotMapped]
        public DateTime LastStatCalculation { get; set; }
        [NotMapped]
        public int LastScore { get; set; }
        [NotMapped]
        public DateTime LastActive { get; set; }
        public void StartNewSession()
        {
            KillStreak = 0;
            DeathStreak = 0;
            LastScore = 0;
            SessionScores.Add(0);
        }
        [NotMapped]
        public int SessionScore
        {
            set
            {
                SessionScores[SessionScores.Count - 1] = value;
            }
            get
            {
                return SessionScores.Sum();
            }
        }
        [NotMapped]
        public int RoundScore
        {
            get
            {
                return SessionScores[SessionScores.Count - 1];
            }
        }
        [NotMapped]
        private List<int> SessionScores = new List<int>() { 0 };
    }
}
