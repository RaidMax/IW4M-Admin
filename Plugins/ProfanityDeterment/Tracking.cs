using SharedLibrary.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace ProfanityDeterment
{
    class Tracking
    {
        public Player Client { get; private set; }
        public int Infringements { get; set; }

        public Tracking(Player client)
        {
            Client = client;
            Infringements = 0;
        }  
    }
}
