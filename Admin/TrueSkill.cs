using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin
{
    class TrueSkill
    {
       public static double calculateWinnerMu(Stats originStats, Stats targetStats)
       {
            double Beta = originStats.lastMew / 6;
            double lastSkill = Gaussian(originStats.lastMew, originStats.lastSigma);
            double c = Math.Sqrt((2 * Beta * Beta)  + (originStats.lastSigma * originStats.lastSigma) + (targetStats.lastSigma * targetStats.lastSigma));
            double newMew = originStats.lastMew + ((originStats.lastSigma) * (originStats.lastSigma) / c) * ((originStats.lastMew - targetStats.lastMew) / c);
            return newMew;
       }

       public static double calculateLoserMu(Stats originStats, Stats targetStats)
       {
           double Beta = originStats.lastMew / 6;
           double lastSkill = Gaussian(originStats.lastMew, originStats.lastSigma);
           double c = Math.Sqrt( (2 * Beta * Beta) + (originStats.lastSigma * originStats.lastSigma) + (targetStats.lastSigma * targetStats.lastSigma));
           double newMew = originStats.lastMew - ((targetStats.lastSigma) * (targetStats.lastSigma) / c) * ((originStats.lastMew - targetStats.lastMew) / c);
           return newMew;
       }

       public static double calculateLoserSigma(Stats originStats, Stats targetStats)
       {
           double Beta = originStats.lastMew / 6;
           double lastSkill = Gaussian(originStats.lastMew, originStats.lastSigma);
           double c = ((2 * Beta * Beta) + originStats.lastSigma * originStats.lastSigma) + (targetStats.lastSigma * targetStats.lastSigma);
           double newSigma = originStats.lastSigma * ( 1 - (targetStats.lastSigma) * (targetStats.lastSigma) / c) * ((originStats.lastMew - targetStats.lastMew) / c);
           return newSigma;
       }

       public static double calculateWinnerSigma(Stats originStats, Stats targetStats)
       {
           double Beta = originStats.lastMew / 6;
           double lastSkill = Gaussian(originStats.lastMew, originStats.lastSigma);
           double c = ((2 * Beta * Beta)  + originStats.lastSigma * originStats.lastSigma) + (targetStats.lastSigma * targetStats.lastSigma);
           double newSigma = originStats.lastSigma * (1 - (originStats.lastSigma) * (originStats.lastSigma) / c) * ((originStats.lastMew - targetStats.lastMew) / c);
           return newSigma;
       }



       //https://gist.github.com/tansey/1444070
       public static double Gaussian( double mean, double stddev)
       {

           double y1 = Math.Sqrt(-2.0 * Math.Log(.5)) * Math.Cos(2.0 * Math.PI * .5);
           return y1 * stddev + mean;
       } 
    }
}
