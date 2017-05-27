using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Interfaces
{
    public interface IPenaltyList
    {
        void AddPenalty(Penalty P);
        void RemovePenalty(Penalty P);
        List<Penalty> FindPenalties(Player P);
    }
}
