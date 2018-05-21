using IW4MAdmin.Plugins.Stats.Cheat;
using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Plugins.Stats.Cheat
{
    class DetectionTracking : ITrackable
    {
        EFClientStatistics Stats;
        EFClientKill Hit;
        Strain Strain;

        public DetectionTracking(EFClientStatistics stats, EFClientKill hit, Strain strain)
        {
            Stats = stats;
            Hit = hit;
            Strain = strain;
        }

        public string GetTrackableValue()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SPM = {Stats.SPM}");
            sb.AppendLine($"KDR = {Stats.KDR}");
            sb.AppendLine($"Kills = {Stats.Kills}");
            sb.AppendLine($"Session Score = {Stats.SessionScore}");
            sb.AppendLine($"Elo = {Stats.EloRating}");
            sb.AppendLine($"Max Sess Strain = {Stats.MaxSessionStrain}");
            sb.AppendLine($"MaxStrain = {Stats.MaxStrain}");
            sb.AppendLine($"Avg Offset = {Stats.AverageHitOffset}");
            sb.AppendLine($"TimePlayed, {Stats.TimePlayed}");
            sb.AppendLine($"HitDamage = {Hit.Damage}");
            sb.AppendLine($"HitOrigin = {Hit.KillOrigin}");
            sb.AppendLine($"DeathOrigin = {Hit.DeathOrigin}");
            sb.AppendLine($"ViewAngles = {Hit.ViewAngles}");
            sb.AppendLine($"WeaponId = {Hit.Weapon.ToString()}");
            sb.AppendLine($"Timeoffset = {Hit.TimeOffset}");
            sb.AppendLine($"HitLocation = {Hit.HitLoc.ToString()}");
            sb.AppendLine($"Distance = {Hit.Distance / 0.0254}");
            sb.AppendLine($"HitType = {Hit.DeathType.ToString()}");
            int i = 0;
            foreach (var predictedAngle in Hit.AnglesList)
            {
                sb.AppendLine($"Predicted Angle [{i}] {predictedAngle}");
                i++;
            }
            sb.AppendLine(Strain.GetTrackableValue());
            sb.AppendLine($"VictimId = {Hit.VictimId}");
            sb.AppendLine($"AttackerId = {Hit.AttackerId}");
            return sb.ToString();

        }
    }
}
