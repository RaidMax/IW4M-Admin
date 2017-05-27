using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedLibrary;

namespace IW4MAdmin
{
    class PenaltyList : SharedLibrary.Interfaces.IPenaltyList
    {
        public PenaltyList()
        {
        }

        public void AddPenalty(Penalty P)
        {
            Manager.GetInstance().GetClientDatabase().addBan(P);
        }

        public void RemovePenalty(Penalty P)
        {
            Manager.GetInstance().GetClientDatabase().removeBan(P.npID);
        }

        public List<Penalty> FindPenalties(Player P)
        {
            return Manager.GetInstance().GetClientDatabase().GetClientPenalties(P);
        }

        public List<Penalty> AsChronoList(int offset, int count)
        {
            return Manager.GetInstance().GetClientDatabase().GetPenaltiesChronologically(offset, count);
        }
    }
}
