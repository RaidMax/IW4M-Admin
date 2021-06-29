using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using ILogger = Microsoft.Extensions.Logging.ILogger;

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
            Snap,
            Button
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
        string LastWeapon;
        ILogger Log;
        Strain Strain;
        readonly DateTime ConnectionTime = DateTime.UtcNow;
        private double mapAverageRecoilAmount;
        private double sessionAverageSnapAmount;
        private int sessionSnapHits;
        private EFClientKill lastHit;
        private int validRecoilHitCount;
        private int validButtonHitCount;

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
        
        private static double SnapDistance(Vector3 a, Vector3 b, Vector3 c)
        {
            a = a.FixIW4Angles();
            b = b.FixIW4Angles();
            c = c.FixIW4Angles();
            

            float preserveDirectionAngle(float a1, float b1)
            {
                float difference = b1 - a1;
                while (difference < -180) difference += 360;
                while (difference > 180) difference -= 360;
                return difference;
            }

            var directions = new[]
            {
                new Vector3(preserveDirectionAngle(b.X, a.X),preserveDirectionAngle(b.Y, a.Y), 0),
                new Vector3( preserveDirectionAngle(c.X, b.X), preserveDirectionAngle(c.Y, b.Y), 0)
            };

            var distance = new Vector3(Math.Abs(directions[1].X - directions[0].X),
                Math.Abs(directions[1].Y - directions[0].Y), 0);

            return Math.Sqrt((distance.X * distance.X) + (distance.Y * distance.Y));
        }

        /// <summary>
        /// Analyze kill and see if performed by a cheater
        /// </summary>
        /// <param name="hit">kill performed by the player</param>
        /// <returns>true if detection reached thresholds, false otherwise</returns>
        public IEnumerable<DetectionPenaltyResult> ProcessHit(EFClientKill hit)
        {
            var results = new List<DetectionPenaltyResult>();

            if ((hit.DeathType != (int)IW4Info.MeansOfDeath.MOD_PISTOL_BULLET &&
                hit.DeathType != (int)IW4Info.MeansOfDeath.MOD_RIFLE_BULLET &&
                hit.DeathType != (int)IW4Info.MeansOfDeath.MOD_HEAD_SHOT) ||
                hit.HitLoc == (int)IW4Info.HitLocation.none || hit.TimeOffset - LastOffset < 0 ||
                // hack: prevents false positives
                (LastWeapon != hit.WeaponReference && (hit.TimeOffset - LastOffset) == 50))
            {
                return new[] {new DetectionPenaltyResult()
                {
                    ClientPenalty = EFPenalty.PenaltyType.Any,
                }};
            }

            LastWeapon = hit.WeaponReference;

            HitLocationCount[(IW4Info.HitLocation)hit.HitLoc].Count++;
            HitCount++;

            if (hit.IsKill)
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
                    var currentSnapDistance = SnapDistance(hit.AnglesList[0], hit.AnglesList[1], hit.ViewAngles);
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
                    results.Add(new DetectionPenaltyResult()
                    {
                        ClientPenalty = EFPenalty.PenaltyType.Ban,
                        Value = hitLoc.HitOffsetAverage,
                        HitCount = hitLoc.HitCount,
                        Type = DetectionType.Offset
                    });
                }

                // SESSION
                var sessionHitLoc = HitLocationCount[(IW4Info.HitLocation)hit.HitLoc];
                sessionHitLoc.Offset = (sessionHitLoc.Offset * (sessionHitLoc.Count - 1) + realAgainstPredict) / sessionHitLoc.Count;

                int totalSessionHits = HitLocationCount.Sum(_hit => _hit.Value.Count);
                var weightedSessionAverage = HitLocationCount.Where(_hit => _hit.Value.Count > 0)
                    .Sum(_hit => _hit.Value.Offset * _hit.Value.Count) / totalSessionHits;

                AngleDifferenceAverage = weightedSessionAverage;

                if (weightedSessionAverage > Thresholds.MaxOffset(totalSessionHits) &&
                    totalSessionHits >= (Thresholds.MediumSampleMinKills * 2))
                {
                    results.Add(new DetectionPenaltyResult()
                    {
                        ClientPenalty = EFPenalty.PenaltyType.Ban,
                        Value = weightedSessionAverage,
                        HitCount = HitCount,
                        Type = DetectionType.Offset,
                        Location = (IW4Info.HitLocation)hitLoc.Location
                    });
                }
                
                Log.LogDebug("PredictVsReal={realAgainstPredict}", realAgainstPredict);
            }
            #endregion

            #region STRAIN
            double currentStrain = Strain.GetStrain(hit.Distance / 0.0254, hit.ViewAngles, Math.Max(50, LastOffset == 0 ? 50 : (hit.TimeOffset - LastOffset)));
            Log.LogDebug("Current Strain: {currentStrain}", currentStrain);
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
            bool shouldIgnoreDetection = false;
            try
            {
                shouldIgnoreDetection = Plugin.Config.Configuration().AnticheatConfiguration.IgnoredDetectionSpecification[(Server.Game)hit.GameName][DetectionType.Recoil]
                    .Any(_weaponRegex => Regex.IsMatch(hit.WeaponReference, _weaponRegex));
            }

            catch (KeyNotFoundException)
            {

            }

            if (!shouldIgnoreDetection)
            {
                validRecoilHitCount++;
                hitRecoilAverage = (hit.AnglesList.Sum(_angle => _angle.Z) + hit.ViewAngles.Z) / (hit.AnglesList.Count + 1);
                mapAverageRecoilAmount = (mapAverageRecoilAmount * (validRecoilHitCount - 1) + hitRecoilAverage) / validRecoilHitCount;

                if (validRecoilHitCount >= Thresholds.LowSampleMinKills && Kills > Thresholds.LowSampleMinKillsRecoil && mapAverageRecoilAmount == 0)
                {
                    results.Add(new DetectionPenaltyResult()
                    {
                        ClientPenalty = EFPenalty.PenaltyType.Ban,
                        Value = mapAverageRecoilAmount,
                        HitCount = HitCount,
                        Type = DetectionType.Recoil
                    });
                }
            }
            #endregion

            #region BUTTON
            try
            {
                shouldIgnoreDetection = false;
                shouldIgnoreDetection = Plugin.Config.Configuration().AnticheatConfiguration.IgnoredDetectionSpecification[(Server.Game)hit.GameName][DetectionType.Button]
                    .Any(_weaponRegex => Regex.IsMatch(hit.WeaponReference, _weaponRegex));
            }

            catch (KeyNotFoundException)
            {

            }

            if (!shouldIgnoreDetection)
            {
                validButtonHitCount++;

                double lastDiff = hit.TimeOffset - hit.TimeSinceLastAttack;
                if (validButtonHitCount > 0 && lastDiff <= 0)
                {
                    results.Add(new DetectionPenaltyResult()
                    {
                        ClientPenalty = EFPenalty.PenaltyType.Ban,
                        Value = lastDiff,
                        HitCount = HitCount,
                        Type = DetectionType.Button
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

            try
            {
                shouldIgnoreDetection = false; // reset previous value
                shouldIgnoreDetection = Plugin.Config.Configuration().AnticheatConfiguration.IgnoredDetectionSpecification[(Server.Game)hit.GameName][DetectionType.Chest]
                    .Any(_weaponRegex => Regex.IsMatch(hit.WeaponReference, _weaponRegex));
            }

            catch (KeyNotFoundException)
            {

            }

            if (chestHits >= Thresholds.MediumSampleMinKills && !shouldIgnoreDetection)
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
                ServerId = ClientStats.ServerId,
                SessionAngleOffset = AngleDifferenceAverage,
                RecoilOffset = hitRecoilAverage,
                CurrentSessionLength = (int)(DateTime.UtcNow - ConnectionTime).TotalMinutes,
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
                SessionSPM = Math.Round(ClientStats.SessionSPM, 0),
                StrainAngleBetween = Strain.LastDistance,
                TimeSinceLastEvent = (int)Strain.LastDeltaTime,
                WeaponReference = hit.WeaponReference,
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

            return results;
        }

        public void OnMapChange()
        {
            mapAverageRecoilAmount = 0;
            validRecoilHitCount = 0;
        }
    }
}
