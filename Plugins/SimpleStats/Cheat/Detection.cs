using SharedLibrary.Helpers;
using SharedLibrary.Interfaces;
using SharedLibrary.Objects;
using StatsPlugin.Helpers;
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
        EFClientStatistics ClientStats;
        DateTime LastKill;
        ILogger Log;

        public Detection(ILogger log, EFClientStatistics clientStats)
        {
            Log = log;
            HitLocationCount = new Dictionary<IW4Info.HitLocation, int>();
            foreach (var loc in Enum.GetValues(typeof(IW4Info.HitLocation)))
                HitLocationCount.Add((IW4Info.HitLocation)loc, 0);
            LastKill = DateTime.UtcNow;
            ClientStats = clientStats;
        }

        /// <summary>
        /// Analyze kill and see if performed by a cheater
        /// </summary>
        /// <param name="kill">kill performed by the player</param>
        /// <returns>true if detection reached thresholds, false otherwise</returns>
        public DetectionPenaltyResult ProcessKill(EFClientKill kill)
        {
            if ((kill.DeathType != IW4Info.MeansOfDeath.MOD_PISTOL_BULLET &&
                kill.DeathType != IW4Info.MeansOfDeath.MOD_RIFLE_BULLET &&
                kill.DeathType != IW4Info.MeansOfDeath.MOD_HEAD_SHOT) ||
                kill.HitLoc == IW4Info.HitLocation.none)
                return new DetectionPenaltyResult()
                {
                    ClientPenalty = Penalty.PenaltyType.Any,
                    RatioAmount = 0
                };

            HitLocationCount[kill.HitLoc]++;
            Kills++;
            AverageKillTime = (AverageKillTime + (DateTime.UtcNow - LastKill).TotalSeconds) / Kills;

            #region VIEWANGLES
            double distance = Vector3.Distance(kill.KillOrigin, kill.DeathOrigin);
            double x = kill.KillOrigin.X + distance * Math.Cos(kill.ViewAngles.X.ToRadians()) * Math.Cos(kill.ViewAngles.Y.ToRadians());
            double y = kill.KillOrigin.Y + (distance * Math.Sin(kill.ViewAngles.X.ToRadians()) * Math.Cos(kill.ViewAngles.Y.ToRadians()));
            double z = kill.KillOrigin.Z + distance * Math.Sin((360.0f - kill.ViewAngles.Y).ToRadians());
            var trueVector = Vector3.Subtract(kill.KillOrigin, kill.DeathOrigin);
            var calculatedVector = Vector3.Subtract(kill.KillOrigin, new Vector3((float)x, (float)y, (float)z));
            double angle = trueVector.AngleBetween(calculatedVector);

            if (kill.AdsPercent > 0.5 && kill.Distance > 3)
            {
                var hitLoc = ClientStats.HitLocations
                    .First(hl => hl.Location == kill.HitLoc);
                float previousAverage = hitLoc.HitOffsetAverage;
                double newAverage = (previousAverage * (hitLoc.HitCount - 1) + angle) / hitLoc.HitCount;
                hitLoc.HitOffsetAverage = (float)newAverage;
            }

            #endregion

            #region SESSION_RATIOS
            if (Kills >= Thresholds.LowSampleMinKills)
            {
                double marginOfError = Thresholds.GetMarginOfError(Kills);
                // determine what the max headshot percentage can be for current number of kills
                double lerpAmount = Math.Min(1.0, (Kills - Thresholds.LowSampleMinKills) / (double)(Thresholds.HighSampleMinKills - Thresholds.LowSampleMinKills));
                double maxHeadshotLerpValueForFlag = Thresholds.Lerp(Thresholds.HeadshotRatioThresholdLowSample(2.0), Thresholds.HeadshotRatioThresholdHighSample(2.0), lerpAmount) + marginOfError;
                double maxHeadshotLerpValueForBan = Thresholds.Lerp(Thresholds.HeadshotRatioThresholdLowSample(3.0), Thresholds.HeadshotRatioThresholdHighSample(3.0), lerpAmount) + marginOfError;
                //  determine what the max bone percentage can be for current number of kills
                double maxBoneRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.BoneRatioThresholdLowSample(2.25), Thresholds.BoneRatioThresholdHighSample(2.25), lerpAmount) + marginOfError;
                double maxBoneRatioLerpValueForBan = Thresholds.Lerp(Thresholds.BoneRatioThresholdLowSample(3.25), Thresholds.BoneRatioThresholdHighSample(3.25), lerpAmount) + marginOfError;

                // calculate headshot ratio
                double currentHeadshotRatio = ((HitLocationCount[IW4Info.HitLocation.head] + HitLocationCount[IW4Info.HitLocation.helmet]) / (double)Kills);
                // calculate maximum bone 
                double currentMaxBoneRatio = (HitLocationCount.Values.Select(v => v / (double)Kills).Max());

                var bone = HitLocationCount.FirstOrDefault(b => b.Value == HitLocationCount.Values.Max()).Key;
                #region HEADSHOT_RATIO
                // flag on headshot
                if (currentHeadshotRatio > maxHeadshotLerpValueForFlag)
                {
                    // ban on headshot
                    if (currentHeadshotRatio > maxHeadshotLerpValueForFlag)
                    {
                        AboveThresholdCount++;
                        Log.WriteDebug("**Maximum Headshot Ratio Reached For Ban**");
                        Log.WriteDebug($"ClientId: {kill.AttackerId}");
                        Log.WriteDebug($"**Kills: {Kills}");
                        Log.WriteDebug($"**Ratio {currentHeadshotRatio}");
                        Log.WriteDebug($"**MaxRatio {maxHeadshotLerpValueForFlag}");
                        var sb = new StringBuilder();
                        foreach (var kvp in HitLocationCount)
                            sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        Log.WriteDebug(sb.ToString());
                        Log.WriteDebug($"ThresholdReached: {AboveThresholdCount}");

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Ban,
                            RatioAmount = currentHeadshotRatio,
                            Bone = IW4Info.HitLocation.head,
                            KillCount = Kills
                        };
                    }
                    else
                    {
                        AboveThresholdCount++;
                        Log.WriteDebug("**Maximum Headshot Ratio Reached For Flag**");
                        Log.WriteDebug($"ClientId: {kill.AttackerId}");
                        Log.WriteDebug($"**Kills: {Kills}");
                        Log.WriteDebug($"**Ratio {currentHeadshotRatio}");
                        Log.WriteDebug($"**MaxRatio {maxHeadshotLerpValueForFlag}");
                        var sb = new StringBuilder();
                        foreach (var kvp in HitLocationCount)
                            sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        Log.WriteDebug(sb.ToString());
                        Log.WriteDebug($"ThresholdReached: {AboveThresholdCount}");

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Flag,
                            RatioAmount = currentHeadshotRatio,
                            Bone = IW4Info.HitLocation.head,
                            KillCount = Kills
                        };
                    }
                }
                #endregion

                #region BONE_RATIO
                // flag on bone ratio
                else if (currentMaxBoneRatio > maxBoneRatioLerpValueForFlag)
                {
                    // ban on bone ratio
                    if (currentMaxBoneRatio > maxBoneRatioLerpValueForBan)
                    {
                        Log.WriteDebug("**Maximum Bone Ratio Reached For Ban**");
                        Log.WriteDebug($"ClientId: {kill.AttackerId}");
                        Log.WriteDebug($"**Kills: {Kills}");
                        Log.WriteDebug($"**Ratio {currentMaxBoneRatio}");
                        Log.WriteDebug($"**MaxRatio {maxBoneRatioLerpValueForBan}");
                        var sb = new StringBuilder();
                        foreach (var kvp in HitLocationCount)
                            sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        Log.WriteDebug(sb.ToString());

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Ban,
                            RatioAmount = currentMaxBoneRatio,
                            Bone = bone,
                            KillCount = Kills
                        };
                    }
                    else
                    {
                        Log.WriteDebug("**Maximum Bone Ratio Reached For Flag**");
                        Log.WriteDebug($"ClientId: {kill.AttackerId}");
                        Log.WriteDebug($"**Kills: {Kills}");
                        Log.WriteDebug($"**Ratio {currentMaxBoneRatio}");
                        Log.WriteDebug($"**MaxRatio {maxBoneRatioLerpValueForFlag}");
                        var sb = new StringBuilder();
                        foreach (var kvp in HitLocationCount)
                            sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        Log.WriteDebug(sb.ToString());

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Flag,
                            RatioAmount = currentMaxBoneRatio,
                            Bone = bone,
                            KillCount = Kills
                        };
                    }
                }
                #endregion
            }

            #region CHEST_ABDOMEN_RATIO_SESSION
            int chestKills = HitLocationCount[IW4Info.HitLocation.torso_upper];

            if (chestKills >= Thresholds.MediumSampleMinKills)
            {
                double marginOfError = Thresholds.GetMarginOfError(chestKills);
                double lerpAmount = Math.Min(1.0, (chestKills - Thresholds.LowSampleMinKills) / (double)(Thresholds.HighSampleMinKills - Thresholds.LowSampleMinKills));
                // determine max  acceptable ratio of chest to abdomen kills
                double chestAbdomenRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdLowSample(3), Thresholds.ChestAbdomenRatioThresholdHighSample(3), lerpAmount) + marginOfError;
                double chestAbdomenLerpValueForBan = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdLowSample(4), Thresholds.ChestAbdomenRatioThresholdHighSample(4), lerpAmount) + marginOfError;

                double currentChestAbdomenRatio = HitLocationCount[IW4Info.HitLocation.torso_upper] / (double)HitLocationCount[IW4Info.HitLocation.torso_lower];

                if (currentChestAbdomenRatio > chestAbdomenRatioLerpValueForFlag)
                {

                    if (currentChestAbdomenRatio > chestAbdomenLerpValueForBan && chestKills >= Thresholds.MediumSampleMinKills + 30)
                    {
                        Log.WriteDebug("**Maximum Chest/Abdomen Ratio Reached For Ban**");
                        Log.WriteDebug($"ClientId: {kill.AttackerId}");
                        Log.WriteDebug($"**Chest Kills: {chestKills}");
                        Log.WriteDebug($"**Ratio {currentChestAbdomenRatio}");
                        Log.WriteDebug($"**MaxRatio {chestAbdomenLerpValueForBan}");
                        var sb = new StringBuilder();
                        foreach (var kvp in HitLocationCount)
                            sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        Log.WriteDebug(sb.ToString());
                        //  Log.WriteDebug($"ThresholdReached: {AboveThresholdCount}");

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Ban,
                            RatioAmount = currentChestAbdomenRatio,
                            Bone = 0,
                            KillCount = chestKills
                        };
                    }
                    else
                    {
                        Log.WriteDebug("**Maximum Chest/Abdomen Ratio Reached For Flag**");
                        Log.WriteDebug($"ClientId: {kill.AttackerId}");
                        Log.WriteDebug($"**Chest Kills: {chestKills}");
                        Log.WriteDebug($"**Ratio {currentChestAbdomenRatio}");
                        Log.WriteDebug($"**MaxRatio {chestAbdomenRatioLerpValueForFlag}");
                        var sb = new StringBuilder();
                        foreach (var kvp in HitLocationCount)
                            sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        Log.WriteDebug(sb.ToString());
                        // Log.WriteDebug($"ThresholdReached: {AboveThresholdCount}");

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Flag,
                            RatioAmount = currentChestAbdomenRatio,
                            Bone = 0,
                            KillCount = chestKills
                        };
                    }
                }
            }
            #endregion
            #endregion
            return new DetectionPenaltyResult()
            {
                ClientPenalty = Penalty.PenaltyType.Any,
                RatioAmount = 0
            };
        }

        public DetectionPenaltyResult ProcessTotalRatio(EFClientStatistics stats)
        {
            int totalChestKills = stats.HitLocations.Single(c => c.Location == IW4Info.HitLocation.left_arm_upper).HitCount;

            if (totalChestKills >= 250)
            {
                double marginOfError = Thresholds.GetMarginOfError(totalChestKills);
                double lerpAmount = Math.Min(1.0, (totalChestKills - Thresholds.LowSampleMinKills) / (double)(Thresholds.HighSampleMinKills - Thresholds.LowSampleMinKills));
                // determine max  acceptable ratio of chest to abdomen kills
                double chestAbdomenRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdLowSample(2.25), Thresholds.ChestAbdomenRatioThresholdHighSample(2.25), lerpAmount) + marginOfError;
                double chestAbdomenLerpValueForBan = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdLowSample(3.0), Thresholds.ChestAbdomenRatioThresholdHighSample(3.0), lerpAmount) + marginOfError;

                double currentChestAbdomenRatio = stats.HitLocations.Single(hl => hl.Location == IW4Info.HitLocation.torso_upper).HitCount /
                    stats.HitLocations.Single(hl => hl.Location == IW4Info.HitLocation.torso_lower).HitCount;

                if (currentChestAbdomenRatio > chestAbdomenRatioLerpValueForFlag)
                {

                    if (currentChestAbdomenRatio > chestAbdomenLerpValueForBan)
                    {
                        Log.WriteDebug("**Maximum Lifetime Chest/Abdomen Ratio Reached For Ban**");
                        Log.WriteDebug($"ClientId: {stats.ClientId}");
                        Log.WriteDebug($"**Total Chest Kills: {totalChestKills}");
                        Log.WriteDebug($"**Ratio {currentChestAbdomenRatio}");
                        Log.WriteDebug($"**MaxRatio {chestAbdomenLerpValueForBan}");
                        var sb = new StringBuilder();
                        foreach (var location in stats.HitLocations)
                            sb.Append($"HitLocation: {location.Location} -> {location.HitCount}\r\n");
                        Log.WriteDebug(sb.ToString());
                        //  Log.WriteDebug($"ThresholdReached: {AboveThresholdCount}");

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Ban,
                            RatioAmount = currentChestAbdomenRatio,
                            Bone = IW4Info.HitLocation.torso_upper,
                            KillCount = totalChestKills
                        };
                    }
                    else
                    {
                        Log.WriteDebug("**Maximum Lifetime Chest/Abdomen Ratio Reached For Flag**");
                        Log.WriteDebug($"ClientId: {stats.ClientId}");
                        Log.WriteDebug($"**Total Chest Kills: {totalChestKills}");
                        Log.WriteDebug($"**Ratio {currentChestAbdomenRatio}");
                        Log.WriteDebug($"**MaxRatio {chestAbdomenRatioLerpValueForFlag}");
                        var sb = new StringBuilder();
                        foreach (var location in stats.HitLocations)
                            sb.Append($"HitLocation: {location.Location} -> {location.HitCount}\r\n");
                        Log.WriteDebug(sb.ToString());
                        // Log.WriteDebug($"ThresholdReached: {AboveThresholdCount}");

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Flag,
                            RatioAmount = currentChestAbdomenRatio,
                            Bone = IW4Info.HitLocation.torso_upper,
                            KillCount = totalChestKills
                        };
                    }
                }
            }

            return new DetectionPenaltyResult()
            {
                Bone = IW4Info.HitLocation.none,
                ClientPenalty = Penalty.PenaltyType.Any
            };
        }
    }
}
