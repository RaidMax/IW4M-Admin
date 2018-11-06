using System;
using System.Collections.Generic;
using System.Text;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Objects;

namespace IW4MAdmin.Plugins.ProfanityDeterment
{
    class Tracking
    {
        public EFClient Client { get; private set; }
        public int Infringements { get; set; }

        public Tracking(EFClient client)
        {
            Client = client;
            Infringements = 0;
        }  
    }
}
