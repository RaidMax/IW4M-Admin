using System;

namespace SharedLibrary.Helpers
{
    public class PlayerHistory
    {
        public PlayerHistory(int cNum)
        {
            DateTime t = DateTime.UtcNow;
            When = new DateTime(t.Year, t.Month, t.Day, t.Hour, Math.Min(59, 15 * (int)Math.Round(t.Minute / 15.0)), 0);
            PlayerCount = cNum;
        }

#if DEBUG
        public PlayerHistory(DateTime t, int cNum)
        {
            When = new DateTime(t.Year, t.Month, t.Day, t.Hour, Math.Min(59, 15 * (int)Math.Round(t.Minute / 15.0)), 0);
            PlayerCount = cNum;
        }
#endif 

        private DateTime When;
        private int PlayerCount;

        /// <summary>
        /// Used by CanvasJS as a point on the x axis
        /// </summary>
        public string x
        {
            get
            {
                return When.ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
        }


        /// <summary>
        /// Used by CanvasJS as a point on the y axis
        /// </summary>
        public int y
        {
            get
            {
                return PlayerCount;
            }
        }
    }
}
