using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharedLibrary
{
    public abstract class Command
    {
        public Command(String N, String D, String A, Player.Permission P, int args, bool nT)
        {
            Name = N;
            Description = D;
            Alias = A;
            Permission = P;
            requiredArgNum = args;
            needsTarget = nT;
        }

        //Execute the command
        abstract public void Execute(Event E);

        public String Name { get; private set; }
        public String Description { get; private set; }
        public String Alias { get; private set; }
        public int requiredArgNum { get; private set; }
        public bool needsTarget { get; private set; }
        public Player.Permission Permission { get; private set; }
    }
}
