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
    public class ChatDatabase : _Database
    {
        private string[] CommonWords = new string[] { "for",
"with",
"from",
"about",
"your",
"just",
"into",
"over",
"after",
"that",
"not",
"you",
"this",
"but",
"his",
"they",
"then",
"her",
"she",
"will",
"one",
"all",
"would",
"there",
"their",
"have",
"say",
"get",
"make",
"know",
"take",
"see",
"come",
"think",
"look",
"want",
"can",
"was",
"give",
"use",
"find",
"tell",
"ask",
"work",
"seem",
"feel",
"try",
"leave",
"call",
"good",
"new",
"first",
"last",
"long",
"great",
"little",
"own",
"other",
"old",
"right",
"big",
"high",
"small",
"large",
"next",
"early",
"young",
"important",
"few",
"public",
"same",
"able",
"the",
"and",
"that",
"than",
"have",
"this",
"one",
"would",
 "yeah",
        "yah",
        "why",
        "who" ,
            "when",
        "where",
        };

        public ChatDatabase(string FN, SharedLibrary.Interfaces.ILogger logger) : base(FN, logger)
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
            if (message.Length < 3)
                return;

            var chat = new Dictionary<string, object>()
            {
                { "ClientID", clientID },
                { "ServerID", serverID },
                { "Message", message},
                { "TimeSent", DateTime.UtcNow }
            };

            Insert("CHATHISTORY", chat);

            var eachWord = message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where (word => word.Length >= 3)
                .Where(word => CommonWords.FirstOrDefault(c => c == word.ToLower()) == null)
                .ToList();

            foreach (string _word in eachWord)
            {
                string word = _word.ToLower();
                Insert("WORDSTATS", new Dictionary<string, object>() { { "Word", word } }, true);
                UpdateIncrement("WORDSTATS", "Count", new Dictionary<string, object>() { { "Count", 1 } }, new KeyValuePair<string, object>("Word", word));
            }
        }

        public KeyValuePair<string, int>[] GetWords()
        {
            var result = GetDataTable("SELECT * FROM WORDSTATS ORDER BY Count desc LIMIT 200");
            return result.Select().Select(w => new KeyValuePair<string, int>(w["Word"].ToString(), Convert.ToInt32(w["Count"].ToString()))).ToArray();
        }
    }
}
