namespace Data.Models
{
    public class Reference
    {
        public enum Game
        {
            COD = -1,
            UKN = 0,
            IW3 = 1,
            IW4 = 2,
            IW5 = 3,
            IW6 = 4,
            T4 = 5,
            T5 = 6,
            T6 = 7,
            T7 = 8,
            SHG1 = 9,
            CSGO = 10,
            H1 = 11
        }
        
        public enum ConnectionType
        {
            Connect,
            Disconnect
        }
    }
}
