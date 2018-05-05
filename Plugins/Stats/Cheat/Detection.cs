using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using IW4MAdmin.Plugins.Stats.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IW4MAdmin.Plugins.Stats.Cheat
{
    class Detection
    {
        int Kills;
        int AboveThresholdCount;
        double AverageKillTime;
        Dictionary<IW4Info.HitLocation, int> HitLocationCount;
        EFClientStatistics ClientStats;
        DateTime LastKill;
        long LastOffset;
        ILogger Log;
        Strain Strain;

        public Detection(ILogger log, EFClientStatistics clientStats)
        {
            Log = log;
            HitLocationCount = new Dictionary<IW4Info.HitLocation, int>();
            foreach (var loc in Enum.GetValues(typeof(IW4Info.HitLocation)))
                HitLocationCount.Add((IW4Info.HitLocation)loc, 0);
            ClientStats = clientStats;
            Strain = new Strain();
        }

        public void ProcessDamage(string damageLine)
        {
            string regex = @"^(D);((?:bot[0-9]+)|(?:[A-Z]|[0-9])+);([0-9]+);(axis|allies);(.+);((?:[A-Z]|[0-9])+);([0-9]+);(axis|allies);(.+);((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";

            var match = Regex.Match(damageLine, regex, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var meansOfDeath = ParseEnum<IW4Info.MeansOfDeath>.Get(match.Groups[12].Value, typeof(IW4Info.MeansOfDeath));
                var hitLocation = ParseEnum<IW4Info.HitLocation>.Get(match.Groups[13].Value, typeof(IW4Info.HitLocation));

                if (meansOfDeath == IW4Info.MeansOfDeath.MOD_PISTOL_BULLET ||
                meansOfDeath == IW4Info.MeansOfDeath.MOD_RIFLE_BULLET ||
                meansOfDeath == IW4Info.MeansOfDeath.MOD_HEAD_SHOT)
                {
                    ClientStats.HitLocations.First(hl => hl.Location == hitLocation).HitCount += 1;
                }
            }
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
                };

            if (LastKill == DateTime.MinValue)
                LastKill = DateTime.UtcNow;

            HitLocationCount[kill.HitLoc]++;
            Kills++;
            AverageKillTime = (AverageKillTime + (DateTime.UtcNow - LastKill).TotalSeconds) / Kills;

            #region VIEWANGLES   
            // make sure it's divisible by 2
            if (kill.AnglesList.Count % 2 == 0)
            {
                /*
                double maxDistance = 0;
                for (int i = 0; i < kill.AnglesList.Count - 1; i += 1)
                {
                    // Log.WriteDebug($"Fixed 1 {kill.AnglesList[i]}");
                    // Log.WriteDebug($"Fixed 2 {kill.AnglesList[i + 1]}");

                    // fix max distance
                    double currDistance = Vector3.AbsoluteDistance(kill.AnglesList[i], kill.AnglesList[i + 1]);
                    //Log.WriteDebug($"Distance {currDistance}");
                    if (currDistance > maxDistance)
                    {
                        maxDistance = currDistance;
                    }
                                   if (maxDistance > hitLoc.MaxAngleDistance)
                    hitLoc.MaxAngleDistance = (float)maxDistance;
                }*/

                double realAgainstPredict = Vector3.AbsoluteDistance(kill.ViewAngles, kill.AnglesList[10]);

                var hitLoc = ClientStats.HitLocations
                    .First(hl => hl.Location == kill.HitLoc);
                float previousAverage = hitLoc.HitOffsetAverage;
                double newAverage = (previousAverage * (hitLoc.HitCount - 1) + realAgainstPredict) / hitLoc.HitCount;
                hitLoc.HitOffsetAverage = (float)newAverage;

                if (double.IsNaN(hitLoc.HitOffsetAverage))
                {
                    Log.WriteWarning("[Detection::ProcessKill] HitOffsetAvgerage NaN");
                    Log.WriteDebug($"{previousAverage}-{hitLoc.HitCount}-{hitLoc}-{newAverage}");
                    hitLoc.HitOffsetAverage = 0f;
                }

                var hitlocations = ClientStats.HitLocations
                                        .Where(hl => new List<int>() { 4, 5, 2, 3, }.Contains((int)hl.Location))
                                        .Where(hl => ClientStats.SessionKills > Thresholds.MediumSampleMinKills + 30);

                var validOffsets = ClientStats.HitLocations.Where(hl => hl.HitCount > 0);
                double hitOffsetAverage = validOffsets.Sum(o => o.HitCount * o.HitOffsetAverage) / (double)validOffsets.Sum(o => o.HitCount);

                if (hitOffsetAverage > Thresholds.MaxOffset)
                {
                    return new DetectionPenaltyResult()
                    {
                        ClientPenalty = Penalty.PenaltyType.Ban,
                        RatioAmount = hitOffsetAverage,
                        KillCount = ClientStats.SessionKills,
                    };
                }

#if DEBUG
                Log.WriteDebug($"PredictVsReal={realAgainstPredict}");
#endif
            }

            var currentStrain = Strain.GetStrain(kill.ViewAngles, kill.TimeOffset - LastOffset);
            LastOffset = kill.TimeOffset;

            if (currentStrain > ClientStats.MaxStrain)
            {
                ClientStats.MaxStrain = currentStrain;
            }

            if (Strain.TimesReachedMaxStrain >= 3)
            {
                return new DetectionPenaltyResult()
                {
                    ClientPenalty = Penalty.PenaltyType.Ban,
                    RatioAmount = ClientStats.MaxStrain,
                    KillCount = ClientStats.SessionKills,
                };
            }

#if DEBUG
            Log.WriteDebug($"Current Strain: {currentStrain}");
#endif
            LastKill = kill.When;

            #endregion

            #region SESSION_RATIOS
            if (Kills >= Thresholds.LowSampleMinKills)
            {
                double marginOfError = Thresholds.GetMarginOfError(Kills);
                // determine what the max headshot percentage can be for current number of kills
                double lerpAmount = Math.Min(1.0, (Kills - Thresholds.LowSampleMinKills) / (double)(/*Thresholds.HighSampleMinKills*/ 60 - Thresholds.LowSampleMinKills));
                double maxHeadshotLerpValueForFlag = Thresholds.Lerp(Thresholds.HeadshotRatioThresholdLowSample(2.0), Thresholds.HeadshotRatioThresholdHighSample(2.0), lerpAmount) + marginOfError;
                double maxHeadshotLerpValueForBan = Thresholds.Lerp(Thresholds.HeadshotRatioThresholdLowSample(3.0), Thresholds.HeadshotRatioThresholdHighSample(3.0), lerpAmount) + marginOfError;
                //  determine what the max bone percentage can be for current number of kills
                double maxBoneRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.BoneRatioThresholdLowSample(2.25), Thresholds.BoneRatioThresholdHighSample(2.25), lerpAmount) + marginOfError;
                double maxBoneRatioLerpValueForBan = Thresholds.Lerp(Thresholds.BoneRatioThresholdLowSample(3.25), Thresholds.BoneRatioThresholdHighSample(3.25), lerpAmount) + marginOfError;

                // calculate headshot ratio
                double currentHeadshotRatio = ((HitLocationCount[IW4Info.HitLocation.head] + HitLocationCount[IW4Info.HitLocation.helmet] + HitLocationCount[IW4Info.HitLocation.neck]) / (double)Kills);

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
                double lerpAmount = Math.Min(1.0, (chestKills - Thresholds.MediumSampleMinKills) / (double)(Thresholds.HighSampleMinKills - Thresholds.LowSampleMinKills));
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
            int totalChestKills = stats.HitLocations.Single(c => c.Location == IW4Info.HitLocation.torso_upper).HitCount;

            if (totalChestKills >= 60)
            {
                double marginOfError = Thresholds.GetMarginOfError(totalChestKills);
                double lerpAmount = Math.Min(1.0, (totalChestKills - 60) / 250.0);
                // determine max  acceptable ratio of chest to abdomen kills
                double chestAbdomenRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdHighSample(3.0), Thresholds.ChestAbdomenRatioThresholdHighSample(2.0), lerpAmount) + marginOfError;
                double chestAbdomenLerpValueForBan = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdHighSample(4.0), Thresholds.ChestAbdomenRatioThresholdHighSample(3.0), lerpAmount) + marginOfError;

                double currentChestAbdomenRatio = totalChestKills /
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
