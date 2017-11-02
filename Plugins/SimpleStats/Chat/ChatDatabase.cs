using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary;
using System.IO;
using System.Data;

namespace StatsPlugin
{
    public class ChatDatabase : Database
    {
        public ChatDatabase(string FN) : base(FN)
        {
        }

        public override void Init()
        {
            if (!File.Exists(FileName))
            {
                string createChatHistory = @"CREATE TABLE `CHATHISTORY` (
	                                                                `ClientID`	INTEGER NOT NULL,
	                                                                `Message`	TEXT NOT NULL,
	                                                                `ServerID`	INTEGER NOT NULL,
	                                                                `TimeSent`	TEXT NOT NULL
                                                                    );";

                ExecuteNonQuery(createChatHistory);

                string createChatStats = @"CREATE TABLE `WORDSTATS` (
	                                                            `Word`	TEXT NOT NULL,
	                                                            `Count`	INTEGER NOT NULL DEFAULT 1,
	                                                             PRIMARY KEY(`Word`)
                                                                );";

                ExecuteNonQuery(createChatStats);
            }
        }

        private List<ChatHistory> GetChatHistoryFromQuery(DataTable dt)
        {
            return dt.Select().Select(q => new ChatHistory()
            {
                ClientID = Convert.ToInt32(q["ClientID"].ToString()),
                Message = q["Message"].ToString(),
                ServerID = Convert.ToInt32(q["ServerID"].ToString()),
                TimeSent = DateTime.Parse(q["TimeSent"].ToString())
            })
            .ToList();
        }

        public List<ChatHistory> GetChatForPlayer(int clientID)
        {
            var queryResult = GetDataTable("CHATHISTORY", new KeyValuePair<string, object>("ClientID", clientID));
            return GetChatHistoryFromQuery(queryResult);
        }

        public List<ChatHistory> GetChatForServer(int serverID)
        {
            var queryResult = GetDataTable("CHATHISTORY", new KeyValuePair<string, object>("ServerID", serverID));
            return GetChatHistoryFromQuery(queryResult);
        }

        public void AddChatHistory(int clientID, int serverID, string message)
        {
            var chat = new Dictionary<string, object>()
            {
                { "ClientID", clientID },
                { "ServerID", serverID },
                { "Message", message},
                { "TimeSent", DateTime.UtcNow }
            };

            Insert("CHATHISTORY", chat);

            message.Split(' ').Where(word => word.Length >= 3).Any(word =>
               {
                   word = word.ToLower();
                   Insert("WORDSTATS", new Dictionary<string, object>() { { "Word", word } }, true);
                   // shush :^)
                   ExecuteNonQuery($"UPDATE WORDSTATS SET Count = Count + 1 WHERE Word='{word.CleanChars()}'");
                   return true;
               }
            );
        }

        public KeyValuePair<string, int>[] GetWords()
        {
            var result = GetDataTable("SELECT * FROM WORDSTATS ORDER BY Count desc LIMIT 100");
            return result.Select().Select(w => new KeyValuePair<string, int>(w["Word"].ToString(), Convert.ToInt32(w["Count"].ToString()))).ToArray();
        }
    }
}
