using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageBoard
{
    public class Rank : Identifiable
    {
        public string name;
        public SharedLibrary.Player.Permission equivalentRank;
        public int id;

        /// <summary>
        /// Initial creation
        /// </summary>
        /// <param name="name"></param>
        /// <param name="equivalentRank"></param>
        /// <param name="permissions"></param>
        public Rank(string name, SharedLibrary.Player.Permission equivalentRank)
        {
            this.name = name;
            this.equivalentRank = equivalentRank;
            id = 0;
        }

        public Rank(int id, string name, SharedLibrary.Player.Permission equivalentRank)
        {
            this.name = name;
            this.equivalentRank = equivalentRank;
            this.id = id;
        }

        public int getID()
        {
            return id;
        }
    }

    public class Permission
    {
        [Flags]
        public enum Action
        {
            NONE = 0x0,
            READ = 0x1,
            WRITE = 0x2,
            MODIFY = 0x4,
            DELETE = 0x8
        }

        public int rankID;
        public Action actionable;

        public Permission(int rankID, Action actionable)
        {
            this.rankID = rankID;
            this.actionable = actionable;
        }
    }
}
