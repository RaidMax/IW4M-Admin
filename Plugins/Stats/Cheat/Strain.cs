using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Plugins.Stats.Cheat
{
    class Strain : ITrackable
    {
        private const  double StrainDecayBase = 0.9;
        private double CurrentStrain;
        private Vector3 LastAngle;
        private double LastDeltaTime;
        private double LastDistance;

        public int TimesReachedMaxStrain { get; private set; }

        public double GetStrain(bool isDamage, int damage, double killDistance, Vector3 newAngle, double deltaTime)
        {
            if (LastAngle == null)
                LastAngle = newAngle;

            LastDeltaTime = deltaTime;

            double decayFactor = GetDecay(deltaTime);
            CurrentStrain *= decayFactor;

#if DEBUG
            Console.WriteLine($"Decay Factor = {decayFactor} ");
#endif

            double[] distance = Helpers.Extensions.AngleStuff(newAngle, LastAngle);
            LastDistance = distance[0] + distance[1];

            // this happens on first kill
            if ((distance[0] == 0 && distance[1] == 0) ||
                deltaTime == 0 ||
                double.IsNaN(CurrentStrain))
            {
                return CurrentStrain;
            }

            double newStrain = Math.Pow(distance[0] + distance[1], 0.99) / deltaTime;
            newStrain *= killDistance / 1000.0;

            CurrentStrain += newStrain;

            if (CurrentStrain > Thresholds.MaxStrainBan)
                TimesReachedMaxStrain++;

            LastAngle = newAngle;
            return CurrentStrain;
        }

        public string GetTrackableValue()
        {
            return $"Strain =  {CurrentStrain}\r\n, Angle = {LastAngle}\r\n, Delta Time = {LastDeltaTime}\r\n, Angle Between = {LastDistance}";
        }

        private double GetDecay(double deltaTime) => Math.Pow(StrainDecayBase, Math.Pow(2.0, deltaTime / 250.0) / 1000.0);
    }
}