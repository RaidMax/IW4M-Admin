using SharedLibrary.Interfaces;
using StatsPlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsPlugin.Cheat
{
    class Detection
    {
        int Kills;
        int AboveThresholdCount;
        double AverageKillTime;
        Dictionary<IW4Info.HitLocation, int> HitLocationCount;
        DateTime LastKill;
        ILogger Log;

        public Detection(ILogger log)
        {
            Log = log;
            HitLocationCount = new Dictionary<IW4Info.HitLocation, int>();
            foreach (var loc in Enum.GetValues(typeof(IW4Info.HitLocation)))
                HitLocationCount.Add((IW4Info.HitLocation)loc, 0);
            LastKill = DateTime.UtcNow;
        }

        /// <summary>
        /// Analyze kill and see if performed by a cheater
        /// </summary>
        /// <param name="kill">kill performed by the player</param>
        /// <returns>true if detection reached thresholds, false otherwise</returns>
        public bool ProcessKill(EFClientKill kill)
        {
            if (kill.DeathType != IW4Info.MeansOfDeath.MOD_PISTOL_BULLET && kill.DeathType != IW4Info.MeansOfDeath.MOD_RIFLE_BULLET)
                return false;

            bool thresholdReached = false;

            HitLocationCount[kill.HitLoc]++;
            Kills++;
            AverageKillTime = (AverageKillTime + (DateTime.UtcNow - LastKill).TotalSeconds) / Kills;

            if (Kills > Thresholds.LowSampleMinKills)
            {
                double marginOfError = Thresholds.GetMarginOfError(Kills);
                // determine what the max headshot percentage can be for current number of kills
                double lerpAmount = Math.Min(1.0, (Kills - Thresholds.LowSampleMinKills) / (double)(Thresholds.HighSampleMinKills - Thresholds.LowSampleMinKills));
                double maxHeadshotLerpValue = Thresholds.Lerp( Thresholds.HeadshotRatioThresholdLowSample, Thresholds.HeadshotRatioThresholdHighSample, lerpAmount);
                //  determine what the max bone percentage can be for current number of kills
                double maxBoneRatioLerpValue = Thresholds.Lerp(Thresholds.BoneRatioThresholdLowSample, Thresholds.BoneRatioThresholdHighSample, lerpAmount);
                // calculate headshot ratio
                double headshotRatio = ((HitLocationCount[IW4Info.HitLocation.head] + HitLocationCount[IW4Info.HitLocation.helmet]) / (double)Kills) - marginOfError;
                // calculate maximum bone 
                double maximumBoneRatio = (HitLocationCount.Values.Select(v => v / (double)Kills).Max()) - marginOfError;

                if (headshotRatio > maxHeadshotLerpValue)
                {
                    AboveThresholdCount++;
                    Log.WriteDebug("**Maximum Headshot Ratio Reached**");
                    Log.WriteDebug($"ClientId: {kill.AttackerId}");
                    Log.WriteDebug($"**Kills: {Kills}");
                    Log.WriteDebug($"**Ratio {headshotRatio}");
                    Log.WriteDebug($"**MaxRatio {maxHeadshotLerpValue}");
                    var sb = new StringBuilder();
                    foreach (var kvp in HitLocationCount)
                        sb.Append($"HitLocation: {kvp.Key}     Count: {kvp.Value}");
                    Log.WriteDebug(sb.ToString());
                    Log.WriteDebug($"ThresholdReached: {AboveThresholdCount}");
                    thresholdReached = true;
                }

                else if (maximumBoneRatio > maxBoneRatioLerpValue)
                {
                    Log.WriteDebug("**Maximum Bone Ratio Reached**");
                    Log.WriteDebug($"ClientId: {kill.AttackerId}");
                    Log.WriteDebug($"**Kills: {Kills}");
                    Log.WriteDebug($"**Ratio {maximumBoneRatio}");
                    Log.WriteDebug($"**MaxRatio {maxBoneRatioLerpValue}");
                    var sb = new StringBuilder();
                    foreach (var kvp in HitLocationCount)
                        sb.Append($"HitLocation: {kvp.Key}     Count: {kvp.Value}");
                    Log.WriteDebug(sb.ToString());
                    thresholdReached = true;
                }
            }

            return thresholdReached;
        }
    }
}
