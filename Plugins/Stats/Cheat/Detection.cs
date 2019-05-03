using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IW4MAdmin.Plugins.Stats.Cheat
{
    class Detection
    {
        public enum DetectionType
        {
            Bone,
            Chest,
            Offset,
            Strain
        };

        public ChangeTracking<EFACSnapshot> Tracker { get; private set; }
        public const int QUEUE_COUNT = 10;

        public List<EFClientKill> QueuedHits { get; set; }
        int Kills;
        int HitCount;
        Dictionary<IW4Info.HitLocation, int> HitLocationCount;
        double AngleDifferenceAverage;
        EFClientStatistics ClientStats;
        long LastOffset;
        IW4Info.WeaponName LastWeapon;
        ILogger Log;
        Strain Strain;
        readonly DateTime ConnectionTime = DateTime.UtcNow;

        public Detection(ILogger log, EFClientStatistics clientStats)
        {
            Log = log;
            HitLocationCount = new Dictionary<IW4Info.HitLocation, int>();
            foreach (var loc in Enum.GetValues(typeof(IW4Info.HitLocation)))
            {
                HitLocationCount.Add((IW4Info.HitLocation)loc, 0);
            }

            ClientStats = clientStats;
            Strain = new Strain();
            Tracker = new ChangeTracking<EFACSnapshot>();
            QueuedHits = new List<EFClientKill>();
        }

        /// <summary>
        /// Analyze kill and see if performed by a cheater
        /// </summary>
        /// <param name="hit">kill performed by the player</param>
        /// <returns>true if detection reached thresholds, false otherwise</returns>
        public DetectionPenaltyResult ProcessHit(EFClientKill hit, bool isDamage)
        {
            if ((hit.DeathType != IW4Info.MeansOfDeath.MOD_PISTOL_BULLET &&
                hit.DeathType != IW4Info.MeansOfDeath.MOD_RIFLE_BULLET &&
                hit.DeathType != IW4Info.MeansOfDeath.MOD_HEAD_SHOT) ||
                hit.HitLoc == IW4Info.HitLocation.none || hit.TimeOffset - LastOffset < 0 ||
                // hack: prevents false positives
                (LastWeapon != hit.Weapon && (hit.TimeOffset - LastOffset) == 50))
            {
                return new DetectionPenaltyResult()
                {
                    ClientPenalty = Penalty.PenaltyType.Any,
                };
            }

            DetectionPenaltyResult result = null;
            LastWeapon = hit.Weapon;

            HitLocationCount[hit.HitLoc]++;
            HitCount++;

            if (!isDamage)
            {
                Kills++;
            }

            #region VIEWANGLES   
            if (hit.AnglesList.Count >= 2)
            {
                double realAgainstPredict = Vector3.ViewAngleDistance(hit.AnglesList[0], hit.AnglesList[1], hit.ViewAngles);

                // LIFETIME
                var hitLoc = ClientStats.HitLocations
                    .First(hl => hl.Location == hit.HitLoc);

                float previousAverage = hitLoc.HitOffsetAverage;
                double newAverage = (previousAverage * (hitLoc.HitCount - 1) + realAgainstPredict) / hitLoc.HitCount;
                hitLoc.HitOffsetAverage = (float)newAverage;

                if (hitLoc.HitOffsetAverage > Thresholds.MaxOffset(hitLoc.HitCount) &&
                    hitLoc.HitCount > 100)
                {
                    //Log.WriteDebug("*** Reached Max Lifetime Average for Angle Difference ***");
                    //Log.WriteDebug($"Lifetime Average = {newAverage}");
                    //Log.WriteDebug($"Bone = {hitLoc.Location}");
                    //Log.WriteDebug($"HitCount = {hitLoc.HitCount}");
                    //Log.WriteDebug($"ID = {hit.AttackerId}");

                    result = new DetectionPenaltyResult()
                    {
                        ClientPenalty = Penalty.PenaltyType.Ban,
                        Value = hitLoc.HitOffsetAverage,
                        HitCount = hitLoc.HitCount,
                        Type = DetectionType.Offset
                    };
                }

                // SESSION
                double sessAverage = (AngleDifferenceAverage * (HitCount - 1) + realAgainstPredict) / HitCount;
                AngleDifferenceAverage = sessAverage;

                if (sessAverage > Thresholds.MaxOffset(HitCount) &&
                    HitCount > 30)
                {
                    //Log.WriteDebug("*** Reached Max Session Average for Angle Difference ***");
                    //Log.WriteDebug($"Session Average = {sessAverage}");
                    //Log.WriteDebug($"HitCount = {HitCount}");
                    //Log.WriteDebug($"ID = {hit.AttackerId}");

                    result = new DetectionPenaltyResult()
                    {
                        ClientPenalty = Penalty.PenaltyType.Ban,
                        Value = sessAverage,
                        HitCount = HitCount,
                        Type = DetectionType.Offset,
                        Location = hitLoc.Location
                    };
                }

#if DEBUG
                Log.WriteDebug($"PredictVsReal={realAgainstPredict}");
#endif
            }

            double currentStrain = Strain.GetStrain(isDamage, hit.Damage, hit.Distance / 0.0254, hit.ViewAngles, Math.Max(50, hit.TimeOffset - LastOffset));
#if DEBUG == true
            Log.WriteDebug($"Current Strain: {currentStrain}");
#endif
            LastOffset = hit.TimeOffset;

            if (currentStrain > ClientStats.MaxStrain)
            {
                ClientStats.MaxStrain = currentStrain;
            }

            // flag
            if (currentStrain > Thresholds.MaxStrainFlag)
            {
                result = new DetectionPenaltyResult()
                {
                    ClientPenalty = Penalty.PenaltyType.Flag,
                    Value = currentStrain,
                    HitCount = HitCount,
                    Type = DetectionType.Strain
                };
            }

            // ban
            if (currentStrain > Thresholds.MaxStrainBan &&
                HitCount >= 5)
            {
                result = new DetectionPenaltyResult()
                {
                    ClientPenalty = Penalty.PenaltyType.Ban,
                    Value = currentStrain,
                    HitCount = HitCount,
                    Type = DetectionType.Strain
                };
            }
            #endregion

            #region SESSION_RATIOS
            if (Kills >= Thresholds.LowSampleMinKills)
            {
                double marginOfError = Thresholds.GetMarginOfError(HitCount);
                // determine what the max headshot percentage can be for current number of kills
                double lerpAmount = Math.Min(1.0, (HitCount - Thresholds.LowSampleMinKills) / (double)(/*Thresholds.HighSampleMinKills*/ 60 - Thresholds.LowSampleMinKills));
                double maxHeadshotLerpValueForFlag = Thresholds.Lerp(Thresholds.HeadshotRatioThresholdLowSample(2.0), Thresholds.HeadshotRatioThresholdHighSample(2.0), lerpAmount) + marginOfError;
                double maxHeadshotLerpValueForBan = Thresholds.Lerp(Thresholds.HeadshotRatioThresholdLowSample(3.5), Thresholds.HeadshotRatioThresholdHighSample(3.5), lerpAmount) + marginOfError;
                //  determine what the max bone percentage can be for current number of kills
                double maxBoneRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.BoneRatioThresholdLowSample(2.25), Thresholds.BoneRatioThresholdHighSample(2.25), lerpAmount) + marginOfError;
                double maxBoneRatioLerpValueForBan = Thresholds.Lerp(Thresholds.BoneRatioThresholdLowSample(3.25), Thresholds.BoneRatioThresholdHighSample(3.25), lerpAmount) + marginOfError;

                // calculate headshot ratio
                double currentHeadshotRatio = ((HitLocationCount[IW4Info.HitLocation.head] + HitLocationCount[IW4Info.HitLocation.helmet] + HitLocationCount[IW4Info.HitLocation.neck]) / (double)HitCount);

                // calculate maximum bone 
                double currentMaxBoneRatio = (HitLocationCount.Values.Select(v => v / (double)HitCount).Max());
                var bone = HitLocationCount.FirstOrDefault(b => b.Value == HitLocationCount.Values.Max()).Key;

                #region HEADSHOT_RATIO
                // flag on headshot
                if (currentHeadshotRatio > maxHeadshotLerpValueForFlag)
                {
                    // ban on headshot
                    if (currentHeadshotRatio > maxHeadshotLerpValueForBan)
                    {
                        //Log.WriteDebug("**Maximum Headshot Ratio Reached For Ban**");
                        //Log.WriteDebug($"ClientId: {hit.AttackerId}");
                        //Log.WriteDebug($"**HitCount: {HitCount}");
                        //Log.WriteDebug($"**Ratio {currentHeadshotRatio}");
                        //Log.WriteDebug($"**MaxRatio {maxHeadshotLerpValueForFlag}");
                        //var sb = new StringBuilder();
                        //foreach (var kvp in HitLocationCount)
                        //{
                        //    sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        //}

                        //Log.WriteDebug(sb.ToString());

                        result = new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Ban,
                            Value = currentHeadshotRatio,
                            Location = IW4Info.HitLocation.head,
                            HitCount = HitCount,
                            Type = DetectionType.Bone
                        };
                    }
                    else
                    {
                        //Log.WriteDebug("**Maximum Headshot Ratio Reached For Flag**");
                        //Log.WriteDebug($"ClientId: {hit.AttackerId}");
                        //Log.WriteDebug($"**HitCount: {HitCount}");
                        //Log.WriteDebug($"**Ratio {currentHeadshotRatio}");
                        //Log.WriteDebug($"**MaxRatio {maxHeadshotLerpValueForFlag}");
                        //var sb = new StringBuilder();
                        //foreach (var kvp in HitLocationCount)
                        //{
                        //    sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        //}

                        //Log.WriteDebug(sb.ToString());

                        result = new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Flag,
                            Value = currentHeadshotRatio,
                            Location = IW4Info.HitLocation.head,
                            HitCount = HitCount,
                            Type = DetectionType.Bone
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
                        Log.WriteDebug($"ClientId: {hit.AttackerId}");
                        Log.WriteDebug($"**HitCount: {HitCount}");
                        Log.WriteDebug($"**Ratio {currentMaxBoneRatio}");
                        Log.WriteDebug($"**MaxRatio {maxBoneRatioLerpValueForBan}");
                        var sb = new StringBuilder();
                        foreach (var kvp in HitLocationCount)
                        {
                            sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        }

                        Log.WriteDebug(sb.ToString());

                        result = new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Ban,
                            Value = currentMaxBoneRatio,
                            Location = bone,
                            HitCount = HitCount,
                            Type = DetectionType.Bone
                        };
                    }
                    else
                    {
                        Log.WriteDebug("**Maximum Bone Ratio Reached For Flag**");
                        Log.WriteDebug($"ClientId: {hit.AttackerId}");
                        Log.WriteDebug($"**HitCount: {HitCount}");
                        Log.WriteDebug($"**Ratio {currentMaxBoneRatio}");
                        Log.WriteDebug($"**MaxRatio {maxBoneRatioLerpValueForFlag}");
                        var sb = new StringBuilder();
                        foreach (var kvp in HitLocationCount)
                        {
                            sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        }

                        Log.WriteDebug(sb.ToString());

                        result = new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Flag,
                            Value = currentMaxBoneRatio,
                            Location = bone,
                            HitCount = HitCount,
                            Type = DetectionType.Bone
                        };
                    }
                }
                #endregion
            }

            #region CHEST_ABDOMEN_RATIO_SESSION
            int chestHits = HitLocationCount[IW4Info.HitLocation.torso_upper];

            if (chestHits >= Thresholds.MediumSampleMinKills)
            {
                double marginOfError = Thresholds.GetMarginOfError(chestHits);
                double lerpAmount = Math.Min(1.0, (chestHits - Thresholds.MediumSampleMinKills) / (double)(Thresholds.HighSampleMinKills - Thresholds.LowSampleMinKills));
                // determine max  acceptable ratio of chest to abdomen kills
                double chestAbdomenRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdLowSample(3), Thresholds.ChestAbdomenRatioThresholdHighSample(3), lerpAmount) + marginOfError;
                double chestAbdomenLerpValueForBan = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdLowSample(4), Thresholds.ChestAbdomenRatioThresholdHighSample(4), lerpAmount) + marginOfError;

                double currentChestAbdomenRatio = HitLocationCount[IW4Info.HitLocation.torso_upper] / (double)HitLocationCount[IW4Info.HitLocation.torso_lower];

                if (currentChestAbdomenRatio > chestAbdomenRatioLerpValueForFlag)
                {

                    if (currentChestAbdomenRatio > chestAbdomenLerpValueForBan && chestHits >= Thresholds.MediumSampleMinKills + 30)
                    {
                        //Log.WriteDebug("**Maximum Chest/Abdomen Ratio Reached For Ban**");
                        //Log.WriteDebug($"ClientId: {hit.AttackerId}");
                        //Log.WriteDebug($"**Chest Hits: {chestHits}");
                        //Log.WriteDebug($"**Ratio {currentChestAbdomenRatio}");
                        //Log.WriteDebug($"**MaxRatio {chestAbdomenLerpValueForBan}");
                        //var sb = new StringBuilder();
                        //foreach (var kvp in HitLocationCount)
                        //    sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        //Log.WriteDebug(sb.ToString());

                        result = new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Ban,
                            Value = currentChestAbdomenRatio,
                            Location = IW4Info.HitLocation.torso_upper,
                            Type = DetectionType.Chest,
                            HitCount = chestHits
                        };
                    }
                    else
                    {
                        //Log.WriteDebug("**Maximum Chest/Abdomen Ratio Reached For Flag**");
                        //Log.WriteDebug($"ClientId: {hit.AttackerId}");
                        //Log.WriteDebug($"**Chest Hits: {chestHits}");
                        //Log.WriteDebug($"**Ratio {currentChestAbdomenRatio}");
                        //Log.WriteDebug($"**MaxRatio {chestAbdomenRatioLerpValueForFlag}");
                        //var sb = new StringBuilder();
                        //foreach (var kvp in HitLocationCount)
                        //    sb.Append($"HitLocation: {kvp.Key} -> {kvp.Value}\r\n");
                        //Log.WriteDebug(sb.ToString());

                        result = new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Flag,
                            Value = currentChestAbdomenRatio,
                            Location = IW4Info.HitLocation.torso_upper,
                            Type = DetectionType.Chest,
                            HitCount = chestHits
                        };
                    }
                }
            }
            #endregion
            #endregion

            Tracker.OnChange(new EFACSnapshot()
            {
                When = hit.When,
                ClientId = ClientStats.ClientId,
                SessionAngleOffset = AngleDifferenceAverage,
                CurrentSessionLength = (int)(DateTime.UtcNow - ConnectionTime).TotalSeconds,
                CurrentStrain = currentStrain,
                CurrentViewAngle = hit.ViewAngles,
                Hits = HitCount,
                Kills = Kills,
                Deaths = ClientStats.SessionDeaths,
                HitDestinationId = hit.DeathOrigin.Vector3Id,
                HitDestination = hit.DeathOrigin,
                HitOriginId = hit.KillOrigin.Vector3Id,
                HitOrigin = hit.KillOrigin,
                EloRating = ClientStats.EloRating,
                HitLocation = hit.HitLoc,
                LastStrainAngle = Strain.LastAngle,
                PredictedViewAngles = hit.AnglesList,
                // this is in "meters"
                Distance = hit.Distance,
                SessionScore = ClientStats.SessionScore,
                HitType = hit.DeathType,
                SessionSPM = ClientStats.SessionSPM,
                StrainAngleBetween = Strain.LastDistance,
                TimeSinceLastEvent = (int)Strain.LastDeltaTime,
                WeaponId = hit.Weapon
            });

            return result ?? new DetectionPenaltyResult()
            {
                ClientPenalty = Penalty.PenaltyType.Any,
            };
        }

        public DetectionPenaltyResult ProcessTotalRatio(EFClientStatistics stats)
        {
            int totalChestHits = stats.HitLocations.Single(c => c.Location == IW4Info.HitLocation.torso_upper).HitCount;

            if (totalChestHits >= 60)
            {
                double marginOfError = Thresholds.GetMarginOfError(totalChestHits);
                double lerpAmount = Math.Min(1.0, (totalChestHits - 60) / 250.0);
                // determine max  acceptable ratio of chest to abdomen kills
                double chestAbdomenRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdHighSample(3.0), Thresholds.ChestAbdomenRatioThresholdHighSample(2.0), lerpAmount) + marginOfError;
                double chestAbdomenLerpValueForBan = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdHighSample(4.0), Thresholds.ChestAbdomenRatioThresholdHighSample(3.0), lerpAmount) + marginOfError;

                double currentChestAbdomenRatio = totalChestHits /
                    stats.HitLocations.Single(hl => hl.Location == IW4Info.HitLocation.torso_lower).HitCount;

                if (currentChestAbdomenRatio > chestAbdomenRatioLerpValueForFlag)
                {

                    if (currentChestAbdomenRatio > chestAbdomenLerpValueForBan)
                    {
                        //Log.WriteDebug("**Maximum Lifetime Chest/Abdomen Ratio Reached For Ban**");
                        //Log.WriteDebug($"ClientId: {stats.ClientId}");
                        //Log.WriteDebug($"**Total Chest Hits: {totalChestHits}");
                        //Log.WriteDebug($"**Ratio {currentChestAbdomenRatio}");
                        //Log.WriteDebug($"**MaxRatio {chestAbdomenLerpValueForBan}");
                        //var sb = new StringBuilder();
                        //foreach (var location in stats.HitLocations)
                        //    sb.Append($"HitLocation: {location.Location} -> {location.HitCount}\r\n");
                        //Log.WriteDebug(sb.ToString());

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Ban,
                            Value = currentChestAbdomenRatio,
                            Location = IW4Info.HitLocation.torso_upper,
                            HitCount = totalChestHits,
                            Type = DetectionType.Chest
                        };
                    }
                    else
                    {
                        //Log.WriteDebug("**Maximum Lifetime Chest/Abdomen Ratio Reached For Flag**");
                        //Log.WriteDebug($"ClientId: {stats.ClientId}");
                        //Log.WriteDebug($"**Total Chest Hits: {totalChestHits}");
                        //Log.WriteDebug($"**Ratio {currentChestAbdomenRatio}");
                        //Log.WriteDebug($"**MaxRatio {chestAbdomenRatioLerpValueForFlag}");
                        //var sb = new StringBuilder();
                        //foreach (var location in stats.HitLocations)
                        //    sb.Append($"HitLocation: {location.Location} -> {location.HitCount}\r\n");
                        //Log.WriteDebug(sb.ToString());

                        return new DetectionPenaltyResult()
                        {
                            ClientPenalty = Penalty.PenaltyType.Flag,
                            Value = currentChestAbdomenRatio,
                            Location = IW4Info.HitLocation.torso_upper,
                            HitCount = totalChestHits,
                            Type = DetectionType.Chest
                        };
                    }
                }
            }

            return new DetectionPenaltyResult()
            {
                ClientPenalty = Penalty.PenaltyType.Any
            };
        }
    }
}
