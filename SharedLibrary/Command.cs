using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            RequiredArgumentCount = args;
            RequiresTarget = nT;
        }

        //Execute the command
        abstract public Task ExecuteAsync(Event E);

        public String Name { get; private set; }
        public String Description { get; private set; }
        public String Alias { get; private set; }
        public int RequiredArgumentCount { get; private set; }
        public bool RequiresTarget { get; private set; }
        public Player.Permission Permission { get; private set; }
    }
}
