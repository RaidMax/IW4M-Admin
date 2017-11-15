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
            ApplicationManager.GetInstance().GetClientDatabase().AddPenalty(P);
        }

        public void RemovePenalty(Penalty P)
        {
            ApplicationManager.GetInstance().GetClientDatabase().RemoveBan(P.OffenderID);
        }

        public List<Penalty> FindPenalties(Player P)
        {
            return ApplicationManager.GetInstance().GetClientDatabase().GetClientPenalties(P);
        }

        public List<Penalty> AsChronoList(int offset, int count, Penalty.Type penaltyType  = Penalty.Type.Any)
        {
            return ApplicationManager.GetInstance().GetClientDatabase().GetPenaltiesChronologically(offset, count, penaltyType);
        }
    }
}
