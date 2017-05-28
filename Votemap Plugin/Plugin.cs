using System;
using System.Collections.Generic;

using SharedLibrary;
using SharedLibrary.Network;
using SharedLibrary.Interfaces;
using System.Threading.Tasks;
using System.Threading;

namespace Votemap_Plugin
{
    /// <summary>
    /// Allow clients to vote for the next map at the end of a round
    /// Map choices are defined in the server
    /// </summary>
    public class VoteMap : Command
    {
        public VoteMap() : base("vote", "vote for the next map. syntax !v <mapname>", "v", Player.Permission.User, 1, false) { }

        /// <summary>
        /// Properties of Event E
        /// Owner: The server the event came from
        /// Origin: The player generating the event
        /// Target: Optional target the player specified
        /// Data: Chat message which triggered the command event
        /// </summary>
        /// <param name="E">This is the `say` event that comes from the server</param>
        public override async Task ExecuteAsync(Event E)
        {
            var voting = Vote.getServerVotes(E.Owner.getPort());

            // we only want to allow a vote during a vote session
            if (voting.voteInSession)
            {
                if (voting.hasVoted(E.Origin.npID))
                    await E.Origin.Tell("You have already voted. Use ^5!vc ^7to ^5cancel ^7your vote");
                else
                {
                    string mapSearch = E.Data.ToLower().Trim();
                    // probably not the most optimized way to match the map.. but nothing is time critical here
                    Map votedMap = E.Owner.maps.Find(m => (m.Alias.ToLower().Contains(mapSearch) || m.Name.Contains(mapSearch)));
                    if (votedMap == null)
                       await  E.Origin.Tell("^1" + E.Data + " is not a recognized map");
                    else
                    {
                        voting.castVote(E.Origin.npID, votedMap);
                        await E.Origin.Tell("You voted for ^5" + votedMap.Alias);
                    }
                }
            }

            else
                await E.Origin.Tell("There is no vote in session");
        }
    }

    public class VoteCancel : Command
    {
        public VoteCancel() : base("votecancel", "cancel your vote for the next map. syntax !vc", "vc", Player.Permission.User, 0, false) { }

        public override async Task  ExecuteAsync(Event E)
        {
            var voting = Vote.getServerVotes(E.Owner.getPort());

            if (voting.voteInSession)
            {
                if (voting.hasVoted(E.Origin.npID))
                {
                    voting.cancelVote(E.Origin.npID);
                    await E.Origin.Tell("Vote cancelled");
                }
                    
                else
                {
                    await E.Origin.Tell("You have no vote to cancel");
                }
            }

            else
                await E.Origin.Tell("There is no vote in session");
        }
    }

    public class Vote : IPlugin
    {
        public class VoteData
        {
            public string guid;
            public Map map;
        }

        public class MapResult
        {
            public Map map;
            public int voteNum;
        }

        public class ServerVoting
        {
            public int serverID
            {
                get; private set;
            }
            public bool voteInSession;
            public bool matchEnded;
            public bool votePassed;
            public bool waitForLoad;
            public DateTime voteTimeStart;
            public DateTime loadStartTime;
            public List<VoteData> voteList
            {
                get; private set;
            }

            public ServerVoting(int id)
            {
                serverID        = id;
                voteInSession   = false;
                votePassed      = false;
                matchEnded      = false;
                waitForLoad     = true;
                voteList        = new List<VoteData>();
            }

            public int getTotalVotes()
            {
                return voteList.Count;
            }

            public bool hasVoted(string guid)
            {
                return voteList.Exists(x => (x.guid == guid));
            }

            public void castVote(string guid, Map map)
            {
                var vote = new VoteData();
                vote.guid = guid;
                vote.map = map;
                voteList.Add(vote);
            }

            public void cancelVote(string guid)
            {
                voteList.RemoveAll(x => (x.guid == guid));
            }

            public MapResult getTopMap()
            {
                List<MapResult> results = new List<MapResult>();
                MapResult result = new MapResult();
                result.map = new Map("Remain", "Remain");
                result.voteNum = 0;

                foreach (var vote in voteList)
                {
                    if (!results.Exists(x => (x.map.Name == vote.map.Name)))
                    {
                        MapResult newResult = new MapResult();
                        newResult.map = vote.map;
                        newResult.voteNum = 1;
                        results.Add(newResult);
                    }

                    else
                    {
                        var map = results.Find(x => x.map.Name == vote.map.Name);
                        map.voteNum += 1;
                    }
                }

                foreach (var map in results)
                    if (map.voteNum > result.voteNum)
                        result = map;

                return result;
            }

        }

        private static List<ServerVoting> serverVotingList;
        public static int minVotes = 3;

        public string Author
        {
            get
            {
                return "RaidMax";
            }
        }

        public float Version
        {
            get
            {
                return 1.0f;
            }
        }

        public string Name
        {
            get
            {
                return "Votemap Plugin";
            }
        }

        public async Task OnLoadAsync()
        {
            serverVotingList = new List<ServerVoting>();
        }

        public async Task OnUnloadAsync()
        {
            serverVotingList.Clear();
        }

        /// <summary>
        /// The server monitor thread calls this about every 1 second
        /// This is not high-precision, but will run 1 time per second
        /// </summary>
        /// <param name="S"></param>
        public async Task OnTickAsync(Server S)
        {
            var serverVotes = getServerVotes(S.getPort());
            Thread.Sleep(500);

            if (serverVotes != null)
            {

                if ((DateTime.Now - serverVotes.loadStartTime).TotalSeconds < 30 /* || S.getPlayers().Count < 3*/)
                    return;
                else
                    serverVotes.waitForLoad = false;

                // dvar that is set & updated by the game script...
                serverVotes.matchEnded = (await S.GetDvarAsync<int>("scr_gameended")).Value == 1;

                /*
                Console.WriteLine("===========================");
                Console.WriteLine("Match ended->" + serverVotes.matchEnded);
                Console.WriteLine("Vote in session->" + serverVotes.voteInSession);
                Console.WriteLine("Vote passed->" + serverVotes.votePassed);*/

                if (!serverVotes.voteInSession && serverVotes.matchEnded && serverVotes.voteTimeStart == DateTime.MinValue)
                {
                    await S.Broadcast("Voting has started for the ^5next map");
                    await S.Broadcast("Type ^5!v <map> ^7to vote for the nextmap!");
                    serverVotes.voteInSession = true;
                    serverVotes.voteTimeStart = DateTime.Now;
                    return;
                }

                if (!serverVotes.voteInSession && serverVotes.votePassed && (DateTime.Now - serverVotes.voteTimeStart).TotalSeconds > 30)
                {
                    await S.ExecuteCommandAsync("map " + serverVotes.getTopMap().map.Name);
                    serverVotes.votePassed  = false;
                    return;
                }

                if (serverVotes.voteInSession)
                {
                    if ((DateTime.Now - serverVotes.voteTimeStart).TotalSeconds > 25)
                    {
                        serverVotes.voteInSession = false;

                        MapResult m = serverVotes.getTopMap();
                        await S.Broadcast("Voting has ended!");

                        if (m.voteNum < minVotes && S.getPlayers().Count > 4)
                            await S.Broadcast("Vote map failed. At least ^5" + minVotes + " ^7people must choose the same map");
                        else
                        {
                            await S.Broadcast(String.Format("Next map is ^5{0} ^7- [^2{1}/{2}^7] votes", m.map.Alias, m.voteNum, serverVotes.getTotalVotes()));
                            serverVotes.votePassed = true;
                        }
                    }
                }
            }
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Start)
            {
                serverVotingList.Add(new ServerVoting(S.getPort()));
            }

            if (E.Type == Event.GType.Stop)
            {
                serverVotingList.RemoveAll(x => x.serverID == S.getPort());
            }

            if (E.Type == Event.GType.MapEnd || E.Type == Event.GType.MapChange)
            {
                var serverVotes = getServerVotes(S.getPort());
                serverVotes.voteList.Clear();
                serverVotes.voteTimeStart   = DateTime.MinValue;
                serverVotes.loadStartTime   = DateTime.Now;
                serverVotes.waitForLoad     = true;
            }
        }
 
        public static ServerVoting getServerVotes(int serverID)
        {
            return serverVotingList.Find(x => (x.serverID == serverID));
        }
    }
}
