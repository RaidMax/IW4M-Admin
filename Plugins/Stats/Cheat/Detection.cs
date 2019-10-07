using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IW4MAdmin.Plugins.Stats.Cheat
{
    public class Detection
    {
        public enum DetectionType
        {
            Bone,
            Chest,
            Offset,
            Strain,
            Recoil,
            Snap
        };

        public ChangeTracking<EFACSnapshot> Tracker { get; private set; }
        public const int MIN_HITS_TO_RUN_DETECTION = 5;
        private const int MIN_ANGLE_COUNT = 5;

        public List<EFClientKill> TrackedHits { get; set; }
        int Kills;
        int HitCount;
        Dictionary<IW4Info.HitLocation, HitInfo> HitLocationCount;
        double AngleDifferenceAverage;
        EFClientStatistics ClientStats;
        long LastOffset;
        IW4Info.WeaponName LastWeapon;
        ILogger Log;
        Strain Strain;
        readonly DateTime ConnectionTime = DateTime.UtcNow;
        private double sessionAverageRecoilAmount;
        private double sessionAverageSnapAmount;
        private int sessionSnapHits;
        private EFClientKill lastHit;
        private int validRecoilHitCount;

        private class HitInfo
        {
            public int Count { get; set; }
            public double Offset { get; set; }
        };

        public Detection(ILogger log, EFClientStatistics clientStats)
        {
            Log = log;
            HitLocationCount = new Dictionary<IW4Info.HitLocation, HitInfo>();
            foreach (var loc in Enum.GetValues(typeof(IW4Info.HitLocation)))
            {
                HitLocationCount.Add((IW4Info.HitLocation)loc, new HitInfo());
            }

            ClientStats = clientStats;
            Strain = new Strain();
            Tracker = new ChangeTracking<EFACSnapshot>();
            TrackedHits = new List<EFClientKill>();
        }

        /// <summary>
        /// Analyze kill and see if performed by a cheater
        /// </summary>
        /// <param name="hit">kill performed by the player</param>
        /// <returns>true if detection reached thresholds, false otherwise</returns>
        public DetectionPenaltyResult ProcessHit(EFClientKill hit, bool isDamage)
        {
            var results = new List<DetectionPenaltyResult>();

            if ((hit.DeathType != IW4Info.MeansOfDeath.MOD_PISTOL_BULLET &&
                hit.DeathType != IW4Info.MeansOfDeath.MOD_RIFLE_BULLET &&
                hit.DeathType != IW4Info.MeansOfDeath.MOD_HEAD_SHOT) ||
                hit.HitLoc == IW4Info.HitLocation.none || hit.TimeOffset - LastOffset < 0 ||
                // hack: prevents false positives
                (LastWeapon != hit.Weapon && (hit.TimeOffset - LastOffset) == 50))
            {
                return new DetectionPenaltyResult()
                {
                    ClientPenalty = EFPenalty.PenaltyType.Any,
                };
            }

            LastWeapon = hit.Weapon;

            HitLocationCount[hit.HitLoc].Count++;
            HitCount++;


            if (!isDamage)
            {
                Kills++;
            }

            #region SNAP
            if (hit.AnglesList.Count == MIN_ANGLE_COUNT)
            {
                if (lastHit == null)
                {
                    lastHit = hit;
                }

                bool areAnglesInvalid = hit.AnglesList[0].Equals(hit.AnglesList[1]) && hit.AnglesList[3].Equals(hit.AnglesList[4]);

                if ((lastHit == hit ||
                    lastHit.VictimId != hit.VictimId ||
                    (hit.TimeOffset - lastHit.TimeOffset) >= 1000) &&
                    !areAnglesInvalid)
                {
                    ClientStats.SnapHitCount++;
                    sessionSnapHits++;
                    var currentSnapDistance = Vector3.SnapDistance(hit.AnglesList[0], hit.AnglesList[1], hit.ViewAngles);
                    double previousAverage = ClientStats.AverageSnapValue;
                    ClientStats.AverageSnapValue = (previousAverage * (ClientStats.SnapHitCount - 1) + currentSnapDistance) / ClientStats.SnapHitCount;
                    double previousSessionAverage = sessionAverageSnapAmount;
                    sessionAverageSnapAmount = (previousSessionAverage * (sessionSnapHits - 1) + currentSnapDistance) / sessionSnapHits;
                    lastHit = hit;

                    //var marginOfError = Thresholds.GetMarginOfError(sessionSnapHits);
                    //var marginOfErrorLifetime = Thresholds.GetMarginOfError(ClientStats.SnapHitCount);

                    if (sessionSnapHits >= Thresholds.MediumSampleMinKills &&
                        sessionAverageSnapAmount >= Thresholds.SnapFlagValue/* + marginOfError*/)
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Flag,
                            Value = sessionAverageSnapAmount,
                            HitCount = sessionSnapHits,
                            Type = DetectionType.Snap
                        });
                    }

                    if (sessionSnapHits >= Thresholds.MediumSampleMinKills &&
                        sessionAverageSnapAmount >= Thresholds.SnapBanValue/* + marginOfError*/)
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Ban,
                            Value = sessionAverageSnapAmount,
                            HitCount = sessionSnapHits,
                            Type = DetectionType.Snap
                        });
                    }

                    // lifetime
                    if (ClientStats.SnapHitCount >= Thresholds.MediumSampleMinKills * 2 &&
                        ClientStats.AverageSnapValue >= Thresholds.SnapFlagValue/* + marginOfErrorLifetime*/)
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Flag,
                            Value = sessionAverageSnapAmount,
                            HitCount = ClientStats.SnapHitCount,
                            Type = DetectionType.Snap
                        });
                    }

                    if (ClientStats.SnapHitCount >= Thresholds.MediumSampleMinKills * 2 &&
                        ClientStats.AverageSnapValue >= Thresholds.SnapBanValue/* + marginOfErrorLifetime*/)
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Ban,
                            Value = sessionAverageSnapAmount,
                            HitCount = ClientStats.SnapHitCount,
                            Type = DetectionType.Snap
                        });
                    }

                }
            }
            #endregion

            #region VIEWANGLES   
            int totalUsableAngleCount = hit.AnglesList.Count - 1;
            int angleOffsetIndex = totalUsableAngleCount / 2;
            if (hit.AnglesList.Count == 5)
            {
                double realAgainstPredict = Vector3.ViewAngleDistance(hit.AnglesList[angleOffsetIndex - 1], hit.AnglesList[angleOffsetIndex + 1], hit.ViewAngles);

                // LIFETIME
                var hitLoc = ClientStats.HitLocations
                    .First(hl => hl.Location == hit.HitLoc);

                float previousAverage = hitLoc.HitOffsetAverage;
                double newAverage = (previousAverage * (hitLoc.HitCount - 1) + realAgainstPredict) / hitLoc.HitCount;
                hitLoc.HitOffsetAverage = (float)newAverage;

                int totalHits = ClientStats.HitLocations.Sum(_hit => _hit.HitCount);
                var weightedLifetimeAverage = ClientStats.HitLocations.Where(_hit => _hit.HitCount > 0)
                    .Sum(_hit => _hit.HitOffsetAverage * _hit.HitCount) / totalHits;

                if (weightedLifetimeAverage > Thresholds.MaxOffset(totalHits) &&
                    hitLoc.HitCount > 100)
                {
                    //Log.WriteDebug("*** Reached Max Lifetime Average for Angle Difference ***");
                    //Log.WriteDebug($"Lifetime Average = {newAverage}");
                    //Log.WriteDebug($"Bone = {hitLoc.Location}");
                    //Log.WriteDebug($"HitCount = {hitLoc.HitCount}");
                    //Log.WriteDebug($"ID = {hit.AttackerId}");

                    results.Add(new DetectionPenaltyResult()
                    {
                        ClientPenalty = EFPenalty.PenaltyType.Ban,
                        Value = hitLoc.HitOffsetAverage,
                        HitCount = hitLoc.HitCount,
                        Type = DetectionType.Offset
                    });
                }

                // SESSION
                var sessionHitLoc = HitLocationCount[hit.HitLoc];
                sessionHitLoc.Offset = (sessionHitLoc.Offset * (sessionHitLoc.Count - 1) + realAgainstPredict) / sessionHitLoc.Count;

                int totalSessionHits = HitLocationCount.Sum(_hit => _hit.Value.Count);
                var weightedSessionAverage = HitLocationCount.Where(_hit => _hit.Value.Count > 0)
                    .Sum(_hit => _hit.Value.Offset * _hit.Value.Count) / totalSessionHits;

                AngleDifferenceAverage = weightedSessionAverage;

                if (weightedSessionAverage > Thresholds.MaxOffset(totalSessionHits) &&
                    totalSessionHits >= (Thresholds.MediumSampleMinKills * 2))
                {
                    Log.WriteDebug("*** Reached Max Session Average for Angle Difference ***");
                    Log.WriteDebug($"Session Average = {weightedSessionAverage}");
                    Log.WriteDebug($"HitCount = {HitCount}");
                    Log.WriteDebug($"ID = {hit.AttackerId}");

                    results.Add(new DetectionPenaltyResult()
                    {
                        ClientPenalty = EFPenalty.PenaltyType.Ban,
                        Value = weightedSessionAverage,
                        HitCount = HitCount,
                        Type = DetectionType.Offset,
                        Location = hitLoc.Location
                    });
                }

#if DEBUG
                Log.WriteDebug($"PredictVsReal={realAgainstPredict}");
#endif
            }
            #endregion

            #region STRAIN
            double currentStrain = Strain.GetStrain(hit.Distance / 0.0254, hit.ViewAngles, Math.Max(50, LastOffset == 0 ? 50 : (hit.TimeOffset - LastOffset)));
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
                results.Add(new DetectionPenaltyResult()
                {
                    ClientPenalty = EFPenalty.PenaltyType.Flag,
                    Value = currentStrain,
                    HitCount = HitCount,
                    Type = DetectionType.Strain
                });
            }

            // ban
            if (currentStrain > Thresholds.MaxStrainBan &&
                HitCount >= 5)
            {
                results.Add(new DetectionPenaltyResult()
                {
                    ClientPenalty = EFPenalty.PenaltyType.Ban,
                    Value = currentStrain,
                    HitCount = HitCount,
                    Type = DetectionType.Strain
                });
            }
            #endregion

            #region RECOIL
            float hitRecoilAverage = 0;
            if (!Plugin.Config.Configuration().RecoilessWeapons.Any(_weaponRegex => Regex.IsMatch(hit.Weapon.ToString(), _weaponRegex)))
            {
                validRecoilHitCount++;
                hitRecoilAverage = (hit.AnglesList.Sum(_angle => _angle.Z) + hit.ViewAngles.Z) / (hit.AnglesList.Count + 1);
                sessionAverageRecoilAmount = (sessionAverageRecoilAmount * (validRecoilHitCount - 1) + hitRecoilAverage) / validRecoilHitCount;

                if (validRecoilHitCount >= Thresholds.LowSampleMinKills && Kills > Thresholds.LowSampleMinKillsRecoil && sessionAverageRecoilAmount == 0)
                {
                    results.Add(new DetectionPenaltyResult()
                    {
                        ClientPenalty = EFPenalty.PenaltyType.Ban,
                        Value = sessionAverageRecoilAmount,
                        HitCount = HitCount,
                        Type = DetectionType.Recoil
                    });
                }
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
                double currentHeadshotRatio = ((HitLocationCount[IW4Info.HitLocation.head].Count + HitLocationCount[IW4Info.HitLocation.helmet].Count + HitLocationCount[IW4Info.HitLocation.neck].Count) / (double)HitCount);

                // calculate maximum bone 
                double currentMaxBoneRatio = (HitLocationCount.Values.Select(v => v.Count / (double)HitCount).Max());
                var bone = HitLocationCount.FirstOrDefault(b => b.Value.Count == HitLocationCount.Values.Max(_hit => _hit.Count)).Key;

                #region HEADSHOT_RATIO
                // flag on headshot
                if (currentHeadshotRatio > maxHeadshotLerpValueForFlag)
                {
                    // ban on headshot
                    if (currentHeadshotRatio > maxHeadshotLerpValueForBan)
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Ban,
                            Value = currentHeadshotRatio,
                            Location = IW4Info.HitLocation.head,
                            HitCount = HitCount,
                            Type = DetectionType.Bone
                        });
                    }
                    else
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Flag,
                            Value = currentHeadshotRatio,
                            Location = IW4Info.HitLocation.head,
                            HitCount = HitCount,
                            Type = DetectionType.Bone
                        });
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
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Ban,
                            Value = currentMaxBoneRatio,
                            Location = bone,
                            HitCount = HitCount,
                            Type = DetectionType.Bone
                        });
                    }
                    else
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Flag,
                            Value = currentMaxBoneRatio,
                            Location = bone,
                            HitCount = HitCount,
                            Type = DetectionType.Bone
                        });
                    }
                }
                #endregion
            }

            #region CHEST_ABDOMEN_RATIO_SESSION
            int chestHits = HitLocationCount[IW4Info.HitLocation.torso_upper].Count;

            if (chestHits >= Thresholds.MediumSampleMinKills)
            {
                double marginOfError = Thresholds.GetMarginOfError(chestHits);
                double lerpAmount = Math.Min(1.0, (chestHits - Thresholds.MediumSampleMinKills) / (double)(Thresholds.HighSampleMinKills - Thresholds.LowSampleMinKills));
                // determine max  acceptable ratio of chest to abdomen kills
                double chestAbdomenRatioLerpValueForFlag = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdLowSample(3), Thresholds.ChestAbdomenRatioThresholdHighSample(3), lerpAmount) + marginOfError;
                double chestAbdomenLerpValueForBan = Thresholds.Lerp(Thresholds.ChestAbdomenRatioThresholdLowSample(4), Thresholds.ChestAbdomenRatioThresholdHighSample(4), lerpAmount) + marginOfError;

                double currentChestAbdomenRatio = HitLocationCount[IW4Info.HitLocation.torso_upper].Count / (double)HitLocationCount[IW4Info.HitLocation.torso_lower].Count;

                if (currentChestAbdomenRatio > chestAbdomenRatioLerpValueForFlag)
                {

                    if (currentChestAbdomenRatio > chestAbdomenLerpValueForBan && chestHits >= Thresholds.MediumSampleMinKills * 2)
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Ban,
                            Value = currentChestAbdomenRatio,
                            Location = IW4Info.HitLocation.torso_upper,
                            Type = DetectionType.Chest,
                            HitCount = chestHits
                        });
                    }
                    else
                    {
                        results.Add(new DetectionPenaltyResult()
                        {
                            ClientPenalty = EFPenalty.PenaltyType.Flag,
                            Value = currentChestAbdomenRatio,
                            Location = IW4Info.HitLocation.torso_upper,
                            Type = DetectionType.Chest,
                            HitCount = chestHits
                        });
                    }
                }
            }
            #endregion
            #endregion

            var snapshot = new EFACSnapshot()
            {
                When = hit.When,
                ClientId = ClientStats.ClientId,
                SessionAngleOffset = AngleDifferenceAverage,
                RecoilOffset = hitRecoilAverage,
                CurrentSessionLength = (int)(DateTime.UtcNow - ConnectionTime).TotalSeconds,
                CurrentStrain = currentStrain,
                CurrentViewAngle = new Vector3(hit.ViewAngles.X, hit.ViewAngles.Y, hit.ViewAngles.Z),
                Hits = HitCount,
                Kills = Kills,
                Deaths = ClientStats.SessionDeaths,
                //todo[9.1.19]: why does this cause unique failure?
                HitDestination = new Vector3(hit.DeathOrigin.X, hit.DeathOrigin.Y, hit.DeathOrigin.Z),
                HitOrigin = new Vector3(hit.KillOrigin.X, hit.KillOrigin.Y, hit.KillOrigin.Z),
                EloRating = ClientStats.EloRating,
                HitLocation = hit.HitLoc,
                LastStrainAngle = new Vector3(Strain.LastAngle.X, Strain.LastAngle.Y, Strain.LastAngle.Z),
                // this is in "meters"
                Distance = hit.Distance,
                SessionScore = ClientStats.SessionScore,
                HitType = hit.DeathType,
                SessionSPM = ClientStats.SessionSPM,
                StrainAngleBetween = Strain.LastDistance,
                TimeSinceLastEvent = (int)Strain.LastDeltaTime,
                WeaponId = hit.Weapon,
                SessionSnapHits = sessionSnapHits,
                SessionAverageSnapValue = sessionAverageSnapAmount
            };

            snapshot.PredictedViewAngles = hit.AnglesList
                .Select(_angle => new EFACSnapshotVector3()
                {
                    Vector = _angle,
                    Snapshot = snapshot
                })
                .ToList();

            Tracker.OnChange(snapshot);

            return results.FirstOrDefault(_result => _result.ClientPenalty == EFPenalty.PenaltyType.Ban) ??
                results.FirstOrDefault(_result => _result.ClientPenalty == EFPenalty.PenaltyType.Flag) ??
                new DetectionPenaltyResult()
                {
                    ClientPenalty = EFPenalty.PenaltyType.Any,
                };
        }
    }
}
