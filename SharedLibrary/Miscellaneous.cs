using System;

namespace SharedLibrary
{
    public class PlayerHistory
    {
        public PlayerHistory(DateTime w, int cNum)
        {
            When = w;
            Players = cNum;
        }
        public DateTime When { get; private set; }
        public int Players { get; private set; }
    }
}
