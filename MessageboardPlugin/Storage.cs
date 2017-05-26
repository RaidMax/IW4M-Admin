using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace MessageBoard.Storage
{
    class Database : SharedLibrary.Database
    {
        public Database(String FN) : base(FN) { }

        public override void Init()
        {
            if (!System.IO.File.Exists(FileName))
            {
                string createClientTable = @"CREATE TABLE [USERS] ( 
                [id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [ranking] INTEGER DEFAULT 0,
                [username] TEXT NOT NULL,
                [email] TEXT NOT NULL, 
                [passwordhash] TEXT NOT NULL,
                [passwordsalt] TEXT NOT NULL,
                [lastlogin] TEXT NOT NULL,
                [creationdate] TEXT NOT NULL,
                [subscribedthreads] TEXT DEFAULT 0,
                [avatarurl] TEXT
                );";

                string createSessionTable = @"CREATE TABLE [SESSIONS] ( 
                [sessionid] TEXT NOT NULL,
                [sessionuserid] INTEGER NOT NULL,
                FOREIGN KEY(sessionuserid) REFERENCES USERS(id)
                );";

                string createRankingTable = @"CREATE TABLE [RANKS] (
                [id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [name] TEXT UNIQUE NOT NULL,
                [equivalentrank] INTEGER DEFAULT 0
                );";

                string createCategoryTable = @"CREATE TABLE [CATEGORIES] (
                [id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [title] TEXT NOT NULL,
                [description] TEXT NOT NULL,
                [permissions] BLOB
                );";
    
                string createThreadTable = @"CREATE TABLE [THREADS] ( 
                [id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [title] TEXT NOT NULL,
                [categoryid] INTEGER NOT NULL,
                [replies] INTEGER DEFAULT 0,
                [authorid] INTEGER NOT NULL,
                [creationdate] TEXT NOT NULL,
                [updateddate] TEXT NOT NULL,
                [content] TEXT NOT NULL,
                [visible] INTEGER DEFAULT 1,
                FOREIGN KEY(authorid) REFERENCES USERS(id),
                FOREIGN KEY(categoryid) REFERENCES CATEGORIES(id)
                );";

                string createReplyTable = @"CREATE TABLE [REPLIES] (
                [id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [title] TEXT NOT NULL,
                [authorid] INT NOT NULL,
                [threadid] INT NOT NULL,
                [creationdate] TEXT NOT NULL,
                [updateddate] TEXT NOT NULL,
                [content] TEXT NOT NULL,
                [visible] INTEGER DEFAULT 1,
                FOREIGN KEY(authorid) REFERENCES USERS(id),
                FOREIGN KEY(threadid) REFERENCES THREADS(id)
                );";


                ExecuteNonQuery(createClientTable);
                ExecuteNonQuery(createSessionTable);
                ExecuteNonQuery(createRankingTable);
                ExecuteNonQuery(createCategoryTable);
                ExecuteNonQuery(createThreadTable);
                ExecuteNonQuery(createReplyTable);

                Rank guestRank  = new Rank(1, "Guest", SharedLibrary.Player.Permission.User);
                Rank userRank   = new Rank(2, "User", SharedLibrary.Player.Permission.Trusted);
                Rank modRank    = new Rank(3, "Moderator", SharedLibrary.Player.Permission.Moderator);
                Rank adminRank  = new Rank(4, "Administrator", SharedLibrary.Player.Permission.Owner);

                addRank(guestRank);
                addRank(userRank);
                addRank(modRank);
                addRank(adminRank);

                List<Permission> defaultCatPerms = new List<Permission> {
                    new Permission(guestRank.getID(), Permission.Action.READ),
                    new Permission(userRank.getID(), Permission.Action.READ | Permission.Action.WRITE),
                    new Permission(modRank.getID(), Permission.Action.READ | Permission.Action.WRITE | Permission.Action.MODIFY),
                    new Permission(adminRank.getID(), Permission.Action.READ | Permission.Action.WRITE | Permission.Action.MODIFY | Permission.Action.DELETE)
                };

                Category defaultCat = new Category(1, "Default Category", "This is the default category.", defaultCatPerms);
                addCategory(defaultCat);
            }
        }

        #region SESSIONS
        public Session getSession(string sessionID)
        {
            DataTable Result = GetDataTable("SESSIONS", new KeyValuePair<string, object>("sessionid", sessionID));

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                int userID = Int32.Parse(ResponseRow["sessionuserid"].ToString());
                User sessionUser = getUser(userID);

                // this shouldn't happen.. but it might :c
                if (sessionUser == null)
                    return null;

                Session foundSession = new Session(sessionUser, sessionID);
                return foundSession;
            }

            else
                return null;
        }

        public Session setSession(int userID, string sessionID)
        {
            // prevent duplicated tuples
            if (getSession(sessionID) != null)
            {
                updateSession(sessionID, userID);
                return getSession(sessionID);
            }

            Dictionary<String, object> newSession = new Dictionary<String, object>();

            newSession.Add("sessionid", sessionID);
            newSession.Add("sessionuserid", userID);

            Insert("SESSIONS", newSession);

            return getSession(sessionID);
        }

        public bool updateSession(string sessionID, int userID)
        {
            if (getSession(sessionID) == null)
                return false;

            Dictionary<string, object> updatedSession = new Dictionary<string, object>();
            updatedSession.Add("sessionuserid", userID);

            Update("SESSIONS", updatedSession, new KeyValuePair<string, object>("sessionid", sessionID));
            return true;
        }
        #endregion

        #region USERS
        private User getUserFromDataTable(DataTable Result)
        {
            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                int id = Convert.ToInt32(ResponseRow["id"].ToString());
                string passwordHash = ResponseRow["passwordhash"].ToString();
                string passwordSalt = ResponseRow["passwordsalt"].ToString();
                string username = ResponseRow["username"].ToString();
                string email = ResponseRow["email"].ToString();
                DateTime lastLogon = DateTime.Parse(ResponseRow["lastlogin"].ToString());
                DateTime creationDate = DateTime.Parse(ResponseRow["creationdate"].ToString());
                Rank ranking = getRank(Convert.ToInt32(ResponseRow["ranking"]));
                string avatarURL = ResponseRow["avatarurl"].ToString();
                string posts = GetDataTable(String.Format("select (select count(*) from THREADS where authorid = {0}) + (select count(*) from REPLIES where authorid = {0}) as posts;", id)).Rows[0]["posts"].ToString();

                User foundUser = new User(id, passwordHash, passwordSalt, username, email, Convert.ToInt32(posts), lastLogon, creationDate, ranking, avatarURL);
                return foundUser;
            }

            return null;
        }

        private Dictionary<string, object> getDataTableFromUser(User addedUser)
        {
            Dictionary<String, object> newUser = new Dictionary<String, object>();

            newUser.Add("username", addedUser.username);
            newUser.Add("email", addedUser.email);
            newUser.Add("passwordhash", addedUser.getPasswordHash());
            newUser.Add("passwordsalt", addedUser.getPasswordSalt());
            newUser.Add("lastlogin", SharedLibrary.Utilities.DateTimeSQLite(addedUser.lastLogin));
            newUser.Add("creationdate", SharedLibrary.Utilities.DateTimeSQLite(addedUser.creationDate));
            //newUser.Add("subscribedthreads", String.Join<int>(",", addedUser.subscribedThreads));
            newUser.Add("ranking", addedUser.ranking.getID());
            newUser.Add("avatarurl", addedUser.avatarURL);

            return newUser;
        }

        public User getUser(int userid)
        {
            DataTable Result = GetDataTable("USERS", new KeyValuePair<string, object>("id", userid));

            return getUserFromDataTable(Result);
        }

        public User getUser(string username)
        {
            DataTable Result = GetDataTable("USERS", new KeyValuePair<string, object>("username", username));

            return getUserFromDataTable(Result);
        }

        public bool userExists(string username, string email)
        {
            String Query = String.Format("SELECT * FROM USERS WHERE username = '{0}' or email = '{1}'", username, email);
            DataTable Result = GetDataTable(Query);

            return Result.Rows.Count > 0;
        }

        /// <summary>
        /// Returns ID of added user
        /// </summary>
        /// <param name="addedUser"></param>
        /// <param name="userSession"></param>
        /// <returns></returns>
        public User addUser(User addedUser, Session userSession)
        {
            var newUser = getDataTableFromUser(addedUser);
            Insert("USERS", newUser);

            // fixme
            User createdUser = getUser(addedUser.username);
            return createdUser;           
        }

        public bool updateUser(User updatedUser)
        {
            var user = getDataTableFromUser(updatedUser);
            Update("USERS", user, new KeyValuePair<string, object>("id", updatedUser.getID()));

            return true;
        }

        public int getNumUsers()
        {
            var Result = GetDataTable("SELECT COUNT(id) AS userCount FROM `USERS`;");
            return Convert.ToInt32(Result.Rows[0]["userCount"]);
        }
        #endregion

        #region CATEGORIES
        private Category getCategoryFromDataTable(DataTable Result)
        {
            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];

                int id = Convert.ToInt32(ResponseRow["id"]);
                string title = ResponseRow["title"].ToString();
                string description = ResponseRow["description"].ToString();
                string permissions = Encoding.UTF8.GetString((byte[])ResponseRow["permissions"]);
                List<Permission> perms = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Permission>>(permissions);

                Category requestedCategory = new Category(id, title, description, perms);
                return requestedCategory;
            }

            return null;
        }

        public void addCategory(Category addingCategory)
        {
            Dictionary<String, object> newCategory = new Dictionary<string, object>();

            newCategory.Add("title", addingCategory.title);
            newCategory.Add("description", addingCategory.description);
            newCategory.Add("permissions", Newtonsoft.Json.JsonConvert.SerializeObject(addingCategory.permissions));

            Insert("CATEGORIES", newCategory);
        }

        public Category getCategory(int id)
        {
            string Query = String.Format("SELECT * FROM CATEGORIES WHERE id = {0}", id);
            DataTable Result = GetDataTable(Query);

            return getCategoryFromDataTable(Result);
        }

        public List<Category> getAllCategories()
        {
            string Query = String.Format("SELECT id FROM CATEGORIES");
            List<Category> cats = new List<Category>();
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                for (int i = 0; i < Result.Rows.Count; i++)
                    cats.Add(getCategory(Convert.ToInt32(Result.Rows[i]["id"])));
            }

            return cats;
        }
        #endregion

        #region THREADS

        public Dictionary<string, object> getDataTableFromThread(ForumThread Thread)
        {
            Dictionary<string, object> newThread = new Dictionary<string, object>();
            newThread.Add("title", Thread.title);
            newThread.Add("categoryid", Thread.threadCategory.getID());
            newThread.Add("replies", Thread.replies);
            newThread.Add("authorid", Thread.author.getID());
            newThread.Add("creationdate", SharedLibrary.Utilities.DateTimeSQLite(Thread.creationDate));
            newThread.Add("updateddate", SharedLibrary.Utilities.DateTimeSQLite(Thread.updatedDate));
            newThread.Add("content", Thread.content);
            newThread.Add("visible", Convert.ToInt32(Thread.visible));

            return newThread;
        }

        public int addThread(ForumThread Thread)
        {
            Insert("THREADS", getDataTableFromThread(Thread));
            return getThreadID(Thread.creationDate);
        }


        public bool updateThread(ForumThread updatedThread)
        {
            var user = getDataTableFromThread(updatedThread);
            Update("THREADS", user, new KeyValuePair<string, object>("id", updatedThread.getID()));

            return true;
        }

        public ForumThread getThread(int id)
        {
            DataTable Result = GetDataTable("THREADS", new KeyValuePair<string, object>("id", id));

            return getThreadFromDataTable(Result);     
        }

        private ForumThread getThreadFromDataTable(DataTable Result)
        {
            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                int id = Convert.ToInt32(ResponseRow["id"].ToString());
                int categoryid = Convert.ToInt32(ResponseRow["categoryid"].ToString());
                int authorid = Convert.ToInt32(ResponseRow["authorid"].ToString());
                int replies = Convert.ToInt32(ResponseRow["replies"].ToString());
                string title = ResponseRow["title"].ToString();

                var category = getCategory(categoryid);
                var author = getUser(authorid);

                bool visible = Convert.ToBoolean((Convert.ToInt32(ResponseRow["visible"])));

                DateTime creationDate = DateTime.Parse(ResponseRow["creationdate"].ToString());
                DateTime updatedDate = DateTime.Parse(ResponseRow["updateddate"].ToString());
                string content = ResponseRow["content"].ToString();

                ForumThread retrievedThread = new ForumThread(id, title, visible, content, replies, author, category, creationDate, updatedDate);
                return retrievedThread;
            }

            return null;
        }

        // we have no other unique id yet
        private int getThreadID(DateTime creationDate)
        {
            string Query = String.Format("SELECT * FROM THREADS WHERE creationdate = \"{0}\"", SharedLibrary.Utilities.DateTimeSQLite(creationDate));
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
                return Convert.ToInt32(Result.Rows[0]["id"].ToString());

            return 0;
        }

        public List<ForumThread> getRecentThreads(int categoryID)
        {
            List<ForumThread> threads = new List<ForumThread>();
            string Query = String.Format("SELECT id FROM THREADS WHERE categoryid = {0} AND visible = 1 ORDER BY `updateddate` DESC LIMIT 3", categoryID);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                for (int i = 0; i < Result.Rows.Count; i++)
                    threads.Add(getThread(Convert.ToInt32(Result.Rows[i]["id"])));
            }

            return threads;
        }

        public List<ForumThread> getCategoryThreads(int categoryID)
        {
            List<ForumThread> threads = new List<ForumThread>();
            string Query = String.Format("SELECT id FROM THREADS WHERE categoryid = {0} and visible = 1 ORDER BY `updateddate` DESC", categoryID);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                for (int i = 0; i < Result.Rows.Count; i++)
                    threads.Add(getThread(Convert.ToInt32(Result.Rows[i]["id"])));
            }

            return threads;
        }
        #endregion

        #region RANKING
        public int addRank(Rank newRank)
        {
            Dictionary<string, object> rank = new Dictionary<string, object>();
            rank.Add("name", newRank.name);
            rank.Add("equivalentrank", (int)newRank.equivalentRank);

            Insert("RANKS", rank);

            Rank r = getRank(newRank.name);

            if (r == null)
                return 0;

            return r.getID();
        }

        public Rank getRank(string rankName)
        {
            DataTable Result = GetDataTable("RANKS", new KeyValuePair<string, object>("name", rankName));

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                string name = ResponseRow["name"].ToString();
                int equivRank = Convert.ToInt32(ResponseRow["equivalentrank"].ToString());
                int id = Convert.ToInt32(ResponseRow["id"].ToString());

                Rank retrievedRank = new Rank(id, name, (SharedLibrary.Player.Permission)equivRank);
                return retrievedRank;
            }

            return null;
        }

        public Rank getRank(int rankID)
        {
            DataTable Result = GetDataTable("RANKS", new KeyValuePair<string, object>("id", rankID));

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                string name = ResponseRow["name"].ToString();
                int equivRank = Convert.ToInt32(ResponseRow["equivalentrank"].ToString());

                Rank retrievedRank = new Rank(rankID, name, (SharedLibrary.Player.Permission)equivRank);
                return retrievedRank;
            }

            return null;
        }
        #endregion

        #region REPLIES
        public int addReply(Post reply)
        {
            Insert("REPLIES", getDataTableFromReply(reply));
            return getReplyID(reply.creationDate);
        }

        public bool updateReply(Post reply)
        {
            return Update("REPLIES", getDataTableFromReply(reply), new KeyValuePair<string, object>("id", reply.id));
        }

        public Post getReply(int id)
        {
            DataTable Result = GetDataTable("REPLIES", new KeyValuePair<string, object>("id", id));

            return getReplyFromDataTable(Result);
        }

        public List<Post> getRepliesFromThreadID(int threadID)
        {
            List<Post> replies = new List<Post>();
            //var Result = GetDataTable("REPLIES", new KeyValuePair<string, object>("threadid", threadID));
            var Result = GetDataTable("SELECT * FROM REPLIES WHERE threadid = " + threadID + " AND visible = 1");

            foreach (DataRow row in Result.Rows)
            {
                replies.Add(getReply(Convert.ToInt32(row["id"].ToString())));
            }

            return replies;
        }

        private Dictionary<string, object> getDataTableFromReply(Post reply)
        {
            Dictionary<string, object> newReply = new Dictionary<string, object>();
            newReply.Add("title", reply.title);
            newReply.Add("authorid", reply.author.getID());
            newReply.Add("threadid", reply.threadid);
            newReply.Add("creationdate", SharedLibrary.Utilities.DateTimeSQLite(reply.creationDate));
            newReply.Add("updateddate", SharedLibrary.Utilities.DateTimeSQLite(reply.updatedDate));
            newReply.Add("content", reply.content);
            newReply.Add("visible", Convert.ToInt32(reply.visible));

            return newReply;
        }

        private Post getReplyFromDataTable(DataTable Result)
        {
            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                int id = Convert.ToInt32(ResponseRow["id"].ToString());
                int threadid = Convert.ToInt32(ResponseRow["threadid"].ToString());
                int authorid = Convert.ToInt32(ResponseRow["authorid"].ToString());
                string title = ResponseRow["title"].ToString();
                var author = getUser(authorid);

                DateTime creationDate = DateTime.Parse(ResponseRow["creationdate"].ToString());
                DateTime updatedDate = DateTime.Parse(ResponseRow["updateddate"].ToString());
                string content = ResponseRow["content"].ToString();

                bool visible = Convert.ToBoolean((Convert.ToInt32(ResponseRow["visible"])));

                Post retrievedPost = new Post(id, threadid, visible, title, content, author, creationDate, updatedDate);
                return retrievedPost;
            }

            return null;
        }

        // we have no other unique id yet
        private int getReplyID(DateTime creationDate)
        {
            DataTable Result = GetDataTable("REPLIES", new KeyValuePair<string, object>("creationdate", SharedLibrary.Utilities.DateTimeSQLite(creationDate)));

            if (Result != null && Result.Rows.Count > 0)
                return Convert.ToInt32(Result.Rows[0]["id"].ToString());

            return 0;
        }
        #endregion
    }
}
