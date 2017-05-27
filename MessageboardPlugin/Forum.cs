using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using SharedLibrary;
using System.Collections.Specialized;


namespace MessageBoard.Forum
{
    public class Manager
    {
        private List<IPage> forumPages;
        private List<Session> activeSessions;
        private Storage.Database database;

        private const int MAX_SESSIONS          = 64;
        public const int TITLE_MAXLENGTH        = 30;
        public const int CONTENT_MAXLENGTH      = 8192;
        public const int USERNAME_MAXLENGTH     = 30;
        public const int PASSWORD_MAXLENGTH     = 64;

        public Rank guestRank;
        public Rank UserRank;
        public Rank ModRank;
        public Rank AdminRank;

        public enum ErrorCode
        {
            NO_ERROR,
            GLOBAL_PERMISSIONDENIED,
            USER_DUPLICATE,
            USER_INVALID,
            USER_BADCREDENTIALS,
            USER_EMPTYCREDENTIALS,
            USER_NOTAUTHORIZED,
            USER_PASSWORDTOOLONG,
            USER_USERNAMETOOLONG,
            USER_BADPROFILEDATA,
            SESSION_INVALID,
            THREAD_BADDATA,
            THREAD_EMPTYDATA,
            THREAD_CONTENTTOOLONG,
            THREAD_TITLETOOLONG,
            THREAD_INVALID,
            REPLY_SAVEFAILED,
            CATEGORY_INVALID,
            CATEGORY_EMPTY,
            USER_MISMATCHEDPASSWORD
        }

        public Manager()
        {
            forumPages = new List<IPage>();
            activeSessions = new List<Session>();
            database = new Storage.Database("forum.db");
        }

        public void startSession(string sessionID)
        {
            try
            {
                Session newSession = getSession(sessionID);
                newSession.sessionStartTime = DateTime.Now;
                //Console.WriteLine("Matching session was found - {0}", sessionID);
                addSession(newSession);
            }

            catch (Exceptions.SessionException)
            {
                //Console.WriteLine("No session was found so we are adding a new one");
                Session newSession = new Session(new User(), sessionID);
                addSession(newSession);
            }
        }

        public Session getSession(string sessionID)
        {
            Session requestedSession = activeSessions.Find(sess => sess.sessionID == sessionID);
            
            if (requestedSession == null)
                requestedSession = database.getSession(sessionID);

            if (requestedSession == null)
                throw new Exceptions.SessionException("Session not found");

            return requestedSession;
        }

        public User getUser(int userID)
        {
            User requestedUser = database.getUser(userID);

            if (requestedUser == null)
                throw new Exceptions.UserException("User not found");

            return requestedUser;
        }

        public User getUser(string username)
        {
            User requestedUser = database.getUser(username);

            if (requestedUser == null)
                throw new Exceptions.UserException("User not found");

            return requestedUser;
        }

        public ForumThread getThread(int threadID)
        {
            ForumThread requestedThread = database.getThread(threadID);

            if (requestedThread == null)
                throw new Exceptions.ThreadException("Thread not found");

            return requestedThread;
        }

        public Post getPost(int postID)
        {
            Post requestedPost = database.getReply(postID);

            if (requestedPost == null)
                throw new Exceptions.ThreadException("Post not found");

            return requestedPost;
        }

        public List<Post> getReplies(int threadID)
        {
            return database.getRepliesFromThreadID(threadID);
        }

        public Post getReply(int replyID)
        {
            Post reply = database.getReply(replyID);

            if (reply == null)
                throw new Exceptions.ThreadException("Reply not found");

            return reply;
        }

        public ErrorCode addPost(ForumThread parentThread, Post newPost)
        {
            int addedPost = database.addReply(newPost);
            if (addedPost > 0)
            {
                parentThread.replies++;
                parentThread.updatedDate = DateTime.Now;
                database.updateThread(parentThread);
                database.updateUser(newPost.author);
                return ErrorCode.NO_ERROR;
            }

            return ErrorCode.REPLY_SAVEFAILED;
        }

        private ErrorCode addSession(Session sess)
        {
            if (activeSessions.Count >= MAX_SESSIONS)
                activeSessions.RemoveAt(0);

            activeSessions.RemoveAll(x => x.sessionUser.ranking.name == "Guest" && x.sessionID == sess.sessionID);

            //Console.WriteLine(String.Format("Adding new session [{0}] [{1}]", sess.sessionID, sess.sessionUser.username));

            if (activeSessions.Find(x => x.sessionID == sess.sessionID) == null)
                activeSessions.Add(sess);

            // if it's a guest session, we don't want to save them in the database...
            if (sess.sessionUser.ranking.name != "Guest")
            {
                database.setSession(sess.sessionUser.id, sess.sessionID);
                sess.sessionUser.lastLogin = DateTime.Now;
                database.updateUser(sess.sessionUser);
            }

            return ErrorCode.NO_ERROR;
        }

        public void removeSession(string sessID)
        {
            activeSessions.RemoveAll(x => x.sessionID == sessID);
            database.removeSession(sessID);
        }

        public ProfileSettings getProfileSettings(int userid)
        {
            var retrieved = database.getProfileSettings(userid);
            if (retrieved == null)
            {
                if (userid > 0)
                {
                    var profile = new ProfileSettings(userid);
                    database.addProfileSettings(profile);
                    return profile;
                }
                return new ProfileSettings(0);
            }
            else
                return retrieved;
        }

        public ErrorCode addUser(User newUser, Session userSession)
        {
            if (database.userExists(newUser.username, newUser.email))
                return ErrorCode.USER_DUPLICATE;

            // first added user is going to be admin
            if (database.getNumUsers() == 0)
                newUser.ranking = AdminRank;

            User createdUser = database.addUser(newUser, userSession);
            database.addProfileSettings(new ProfileSettings(createdUser.id));
            removeSession(userSession.sessionID);
            return addSession(new Session(createdUser, userSession.sessionID));
        }

        public void updateUser(User updatedUser)
        {
            database.updateUser(updatedUser);
        }

        public void updateUserProfile(ProfileSettings updatedUserProfile)
        {
            database.updateProfileSettings(updatedUserProfile);
        }

        public ErrorCode updateThread(ForumThread newThread)
        {
            if (database.updateThread(newThread))
                return ErrorCode.NO_ERROR;

            else
                return ErrorCode.THREAD_INVALID;
        }

        public ErrorCode updateReply(Post updatedReply)
        {
            if (database.updateReply(updatedReply))
                return ErrorCode.NO_ERROR;
            else
                return ErrorCode.THREAD_INVALID;
        }

        public ErrorCode addThread(ForumThread newThread)
        {
            if (database.addThread(newThread) > 0)
                return ErrorCode.NO_ERROR;
            else
                return ErrorCode.THREAD_INVALID;
        }

        public ErrorCode authorizeUser(string username, string password, string sessionID)
        {
            User toAuth = database.getUser(username.ToLower());

            if (toAuth == null)
                return ErrorCode.USER_BADCREDENTIALS;

            bool validCredentials = Encryption.PasswordHasher.VerifyPassword(password, Convert.FromBase64String(toAuth.getPasswordSalt()), Convert.FromBase64String(toAuth.getPasswordHash()));

            if (!validCredentials)
                return ErrorCode.USER_BADCREDENTIALS;

            addSession(new Session(toAuth, sessionID));
            return ErrorCode.NO_ERROR;
        }

        public List<Category> getAllCategories()
        {
            return database.getAllCategories();
        }

        public List<ForumThread> getRecentThreads(int catID)
        {
            return database.getRecentThreads(catID);
        }

        public List<ForumThread> getCategoryThreads(int categoryID)
        {
            return database.getCategoryThreads(categoryID);
        }

        public Category getCategory(int id)
        {
            Category cat = database.getCategory(id);

            if (cat == null)
                throw new Exceptions.CategoryException("Category not found");

            return cat;
        }

        public List<Session> getSessions()
        {
            return activeSessions;
        }

        public void Start()
        {
            var login               = new Pages.Login();
            var loginJSON           = new Pages.LoginJSON();
            var register            = new Pages.Register();
            var registerJSON        = new Pages.RegisterJSON();
            var userinfoJSON        = new Pages.userinfoJSON();
            var viewUser            = new Pages.ViewUser();
            var userCP              = new Pages.UserCP();
            var updateUserJSON      = new Pages.updateUserJSON();
            var categoriesJSON      = new Pages.categoriesJSON();
            var category            = new Pages.ViewCategory();
            var categorythreadsJSON = new Pages.categorythreadsJSON();
            var home                = new Pages.Home();
            var recentthreadsJSON   = new Pages.recentthreadsJSON();
            var postthread          = new Pages.PostThread();
            var postthreadJSON      = new Pages.postthreadJSON();
            var editthreadJSON      = new Pages.editthreadJSON();
            var threadJSON          = new Pages.threadJSON();
            var viewthread          = new Pages.ViewThread();
            var logout              = new Pages.LogOut();
            var stats               = new Pages.StatsJSON();

            forumPages.Add(login);
            forumPages.Add(loginJSON);
            forumPages.Add(register);
            forumPages.Add(registerJSON);
            forumPages.Add(userinfoJSON);
            forumPages.Add(viewUser);
            forumPages.Add(userCP);
            forumPages.Add(updateUserJSON);
            forumPages.Add(categoriesJSON);
            forumPages.Add(category);
            forumPages.Add(categorythreadsJSON);
            forumPages.Add(home);
            forumPages.Add(recentthreadsJSON);
            forumPages.Add(postthread);
            forumPages.Add(postthreadJSON);
            forumPages.Add(editthreadJSON);
            forumPages.Add(threadJSON);
            forumPages.Add(viewthread);
            forumPages.Add(logout);
            forumPages.Add(stats);

            SharedLibrary.WebService.pageList.Add(login);
            SharedLibrary.WebService.pageList.Add(loginJSON);
            SharedLibrary.WebService.pageList.Add(register);
            SharedLibrary.WebService.pageList.Add(registerJSON);
            SharedLibrary.WebService.pageList.Add(userinfoJSON);
            SharedLibrary.WebService.pageList.Add(viewUser);
            SharedLibrary.WebService.pageList.Add(userCP);
            SharedLibrary.WebService.pageList.Add(updateUserJSON);
            SharedLibrary.WebService.pageList.Add(categoriesJSON);
            SharedLibrary.WebService.pageList.Add(category);
            SharedLibrary.WebService.pageList.Add(categorythreadsJSON);
            SharedLibrary.WebService.pageList.Add(home);
            SharedLibrary.WebService.pageList.Add(recentthreadsJSON);
            SharedLibrary.WebService.pageList.Add(postthread);
            SharedLibrary.WebService.pageList.Add(postthreadJSON);
            SharedLibrary.WebService.pageList.Add(editthreadJSON);
            SharedLibrary.WebService.pageList.Add(threadJSON);
            SharedLibrary.WebService.pageList.Add(viewthread);
            SharedLibrary.WebService.pageList.Add(logout);
            SharedLibrary.WebService.pageList.Add(stats);

            guestRank   = database.getRank("Guest");
            UserRank    = database.getRank("User");
            ModRank     = database.getRank("Moderator");
            AdminRank   = database.getRank("Administrator");
        }

        public void Stop()
        {
            //session logouts
            //checkme
            foreach (var page in forumPages)
                SharedLibrary.WebService.pageList.Remove(page);
        }
    }


    public class Pages
    {
        public abstract class JSONPage : IPage
        {
            protected Session currentSession; 

            public bool isVisible()
            {
                return false;
            }
            
            public virtual string getPath()
            {
                return "/forum";
            }

            public string getName()
            {
                return "JSONPage";
            } 

            public virtual HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                HttpResponse resp = new HttpResponse();
                resp.contentType = "application/json";
                resp.additionalHeaders = new Dictionary<string,string>();

                if (requestHeaders.ContainsKey("Cookie"))
                {
                    //Console.WriteLine("JSON request contains session header - " + requestHeaders["Cookie"]);
                    string cookie = requestHeaders["Cookie"].Split('=')[1];
                    Plugin.Main.forum.startSession(cookie);
                    currentSession = Plugin.Main.forum.getSession(cookie);
                }

                else
                {
                    string sessionID = Convert.ToBase64String(Encryption.PasswordHasher.GenerateSalt());
                    resp.additionalHeaders.Add("Set-Cookie", "IW4MAdmin_ForumSession=" + sessionID + "; path=/; expires=Sat, 01 May 2025 12:00:00 GMT");
                    currentSession = new Session(new User(), sessionID);
                    Plugin.Main.forum.startSession(sessionID);
                    currentSession = Plugin.Main.forum.getSession(sessionID);
                }

                return resp;
            }
        }

        abstract public class ForumPage : HTMLPage
        {
            public ForumPage(bool visible) : base(visible) { }
            public abstract override string getName();
            public override string getPath()
            {
                return base.getPath() + "/forum";
            }
            public override Dictionary<string, string> getHeaders(IDictionary<string, string> requestHeaders)
            {
                return base.getHeaders(requestHeaders);
            }

            protected string templatation(string bodyContent)
            {
                StringBuilder S = new StringBuilder();
                S.Append(base.loadHeader());
                S.Append(bodyContent);
                S.Append(base.loadFooter());

                return S.ToString();
            }
        }

        public class Login : ForumPage
        {
            public Login() : base(true)
            {

            }

            public override string getName()
            {
                return "Forum";
            }

            public override string getPath()
            {
                return base.getPath() + "/login";
            }
            
            public override string getContent(NameValueCollection querySet, IDictionary<string,string> headers)
            {
                return templatation(loadFile("forum\\login.html"));
            }
        }

        public class Register : ForumPage
        {
            public Register(): base(false)
            {

            }

            public override string getName()
            {
                return "Register";
            }

            public override string getPath()
            {
                return base.getPath() + "/register";
            }

            public override string getContent(NameValueCollection querySet, IDictionary<string, string> headers)
            {
                string content = loadFile("forum\\register.html");
                return templatation(content);
            }
        }

        public class Home : ForumPage
        {
            public Home() : base(false)
            {

            }

            public override string getName()
            {
                return "Forum - Home";
            }

            public override string getPath()
            {
                return base.getPath() + "/home";
            }

            public override string getContent(NameValueCollection querySet, IDictionary<string, string> headers)
            {
                string content = loadFile("forum\\home.html");
                return templatation(content);
            }
        }

        public class PostThread : ForumPage
        {
            public PostThread() : base(false)
            {

            }

            public override string getName()
            {
                return "Forum - Post New Thread";
            }

            public override string getPath()
            {
                return base.getPath() + "/postthread";
            }

            public override string getContent(NameValueCollection querySet, IDictionary<string, string> headers)
            {
                string content = loadFile("forum\\postthread.html");
                return templatation(content);
            }
        }

        public class ViewCategory : ForumPage
        {
            public ViewCategory() : base(false)
            {

            }

            public override string getName()
            {
                return "Forum - Category View";
            }

            public override string getPath()
            {
                return base.getPath() + "/category";
            }

            public override string getContent(NameValueCollection querySet, IDictionary<string, string> headers)
            {
                string content = loadFile("forum\\category.html");
                return templatation(content);
            }
        }

        public class ViewUser : ForumPage
        {
            public ViewUser() : base(false)
            {

            }

            public override string getName()
            {
                return "Forum - View User";
            }

            public override string getPath()
            {
                return base.getPath() + "/user";
            }

            public override string getContent(NameValueCollection querySet, IDictionary<string, string> headers)
            {
                string content = loadFile("forum\\user.html");
                return templatation(content);
            }
        }

        public class UserCP : ForumPage
        {
            public UserCP() : base(false)
            {

            }

            public override string getName()
            {
                return "Forum - User Control Panel";
            }

            public override string getPath()
            {
                return base.getPath() + "/usercp";
            }

            public override string getContent(NameValueCollection querySet, IDictionary<string, string> headers)
            {
                string content = loadFile("forum\\usercp.html");
                return templatation(content);
            }
        }

        public class ViewThread : ForumPage
        {
            public ViewThread() : base(false)
            {

            }

            public override string getName()
            {
                return "Forum - View Thread";
            }

            public override string getPath()
            {
                return base.getPath() + "/thread";
            }

            public override string getContent(NameValueCollection querySet, IDictionary<string, string> headers)
            {
                string content = loadFile("forum\\thread.html");
                return templatation(content);
            }
        }

        public class LogOut : ForumPage
        {
            public LogOut() : base(false)
            {

            }

            public override string getName()
            {
                return "Forum - Log Out";
            }

            public override string getPath()
            {
                return base.getPath() + "/logout";
            }

            public override Dictionary<string, string> getHeaders(IDictionary<string, string> requestHeaders)
            {
                Plugin.Main.forum.removeSession(requestHeaders["Cookie"].Split('=')[1]);
                return new Dictionary<string, string>() { { "Set-Cookie", "IW4MAdmin_ForumSession=deleted; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT" } };
            }

            public override string getContent(NameValueCollection querySet, IDictionary<string, string> headers)
            {
                string content = @"<meta http-equiv='refresh' content='0; url = login' />";
                return templatation(content);
            }
        }

        public class RegisterJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_register";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
          
                var result = new ActionResponse();
                result.success = false;
                result.destination = base.getPath() + "/error";
                
                try {

                    string username = DNA.Text.TextEngine.Text(querySet["username"]);
                    string password = DNA.Text.TextEngine.Text(querySet["password"]);
                    string email = DNA.Text.TextEngine.Text(querySet["email"]);

                    bool validEmail = Regex.IsMatch(email,
                        @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                        @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                    RegexOptions.IgnoreCase) && email.Length < Manager.PASSWORD_MAXLENGTH;

                    if (!validEmail)
                        throw new Exceptions.UserException("Email is invalid");

                    if (username.Length > Manager.USERNAME_MAXLENGTH)
                        throw new Exceptions.UserException("Username is too long");

                    if (password.Length > Manager.PASSWORD_MAXLENGTH)
                        throw new Exceptions.UserException("Password is too long");

                    byte[] passwordSalt = Encryption.PasswordHasher.GenerateSalt();
                    string b64PasswordHash = Convert.ToBase64String(Encryption.PasswordHasher.ComputeHash(password, passwordSalt));
   
                    User registeringUser = new User(username, querySet["hiddenUsername"], email, b64PasswordHash, Convert.ToBase64String(passwordSalt), Plugin.Main.forum.UserRank);

                    currentSession = new Session(registeringUser, currentSession.sessionID);
                    var addUserResult = Plugin.Main.forum.addUser(registeringUser, currentSession);

                    if (addUserResult != Manager.ErrorCode.NO_ERROR)
                    {
                        result.errorCode = addUserResult;
                    }

                    else
                    {
                        result.destination = base.getPath() + "/home";
                        result.success = true;
                        result.errorCode = Manager.ErrorCode.NO_ERROR;
                    }
                }

                catch (Exception E)
                {
                    //logme
                    result.errorCode = Manager.ErrorCode.USER_INVALID;
                }

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                return resp;
            }
        }

        public class userinfoJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_userinfo";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);

                UserInfo info = new UserInfo();
                bool validUserSelection = true;

                try
                {
                    int userid = Convert.ToInt32(querySet["id"]);
                    info.user = Plugin.Main.forum.getUser(userid);
                    info.profile = Plugin.Main.forum.getProfileSettings(userid);
                }

                catch (FormatException)
                {
                    // logme
                    validUserSelection = false;
                }

                catch (Exceptions.UserException)
                {
                    //logme
                    validUserSelection = false;
                }

                if (!validUserSelection)
                {
                    info.user = currentSession.sessionUser;
                    try
                    {
                        info.profile = Plugin.Main.forum.getProfileSettings(info.user.id);
                    }

                    catch (Exceptions.UserException)
                    {
                        //logme
                    }
                }

                /*// this should not be a thing but ok...
                Player matchedPlayer = Plugin.Main.stupidServer.clientDB.getPlayer(querySet["ip"]);

                if (matchedPlayer != null)
                    info.matchedUsername = matchedPlayer.Name;*/

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(info);
                return resp;
            }
        }

        public class updateUserJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_updateuser";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
                var aResp = new ActionResponse();

                bool passwordUpdateRequest = false;

                if (querySet["username"] == null || currentSession.sessionUser.id == 0)
                    aResp.errorCode = Manager.ErrorCode.USER_INVALID;
                else if (querySet["bannercolor"] == null)
                    aResp.errorCode = Manager.ErrorCode.USER_BADPROFILEDATA;
                if (querySet["updatedpassword"] != null && querySet["updatedpasswordrepeat"] != null && querySet["updatedpassword"].Length > 0 && querySet["updatedpasswordrepeat"].Length > 0)
                    passwordUpdateRequest = true;

                if (aResp.errorCode == Manager.ErrorCode.NO_ERROR)
                {
                    string username = DNA.Text.TextEngine.Text(querySet["username"]);
                    string bannercolor = DNA.Text.TextEngine.Text(querySet["bannercolor"]);
                    string avatarURL = DNA.Text.TextEngine.Text(querySet["avatarurl"]);
                    string password = null;

                    if (passwordUpdateRequest)
                    {
                        password = DNA.Text.TextEngine.Text(querySet["updatedpassword"]);
                        string passwordRepeat = DNA.Text.TextEngine.Text(querySet["updatedpasswordrepeat"]);
                        if (!password.Equals(passwordRepeat))
                        {
                            password = null;
                            aResp.errorCode = Manager.ErrorCode.USER_MISMATCHEDPASSWORD;
                        }

                        else if (password.Length > Manager.PASSWORD_MAXLENGTH)
                        {
                            password = null;
                            aResp.errorCode = Manager.ErrorCode.USER_PASSWORDTOOLONG;
                        }
                    }

                    User existingUser = null;
                    try
                    {
                        existingUser = Plugin.Main.forum.getUser(username);
                    }

                    catch (Exceptions.UserException)
                    {

                    }


                    if (existingUser != null && existingUser.id != currentSession.sessionUser.id)
                        aResp.errorCode = Manager.ErrorCode.USER_DUPLICATE;
                    else
                    {
                        var profile = Plugin.Main.forum.getProfileSettings(currentSession.sessionUser.id);
                        if (username.Length <= Manager.USERNAME_MAXLENGTH)
                            currentSession.sessionUser.updateUsername(username);
                        else
                            aResp.errorCode = Manager.ErrorCode.USER_USERNAMETOOLONG;
                        currentSession.sessionUser.updateAvatar(avatarURL);

                        if (passwordUpdateRequest && aResp.errorCode == Manager.ErrorCode.NO_ERROR)
                        {
                            byte[] passwordSalt = Encryption.PasswordHasher.GenerateSalt();
                            string b64PasswordHash = Convert.ToBase64String(Encryption.PasswordHasher.ComputeHash(password, passwordSalt));
                            currentSession.sessionUser.updatePassword(Convert.ToBase64String(passwordSalt), b64PasswordHash);
                        }

                        Plugin.Main.forum.updateUser(currentSession.sessionUser);
                        if (bannercolor.Length == 7)
                            profile.bannerColor = bannercolor;
                        Plugin.Main.forum.updateUserProfile(profile);
                        
                    }
                }

                aResp.success = aResp.errorCode == Manager.ErrorCode.NO_ERROR;
                if (aResp.success)
                    aResp.destination = "usercp";

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(aResp);
                return resp;          
            }
        }

        public class LoginJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_login";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
                ActionResponse aResp = new ActionResponse();
                aResp.success = false;

                try
                {
                    string username = DNA.Text.TextEngine.Text(querySet["username"]);
                    string password = DNA.Text.TextEngine.Text(querySet["password"]);

                    var result = Plugin.Main.forum.authorizeUser(username, password, currentSession.sessionID);
                    aResp.success = result == Manager.ErrorCode.NO_ERROR;
                    aResp.errorCode = result;
                    aResp.destination = "home";
                }

                catch (KeyNotFoundException)
                {
                    aResp.errorCode = Manager.ErrorCode.USER_EMPTYCREDENTIALS;
                }

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(aResp);
                return resp;
            }
        }

        public class categoriesJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_categories";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
                var categories = Plugin.Main.forum.getAllCategories();
     

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(categories);
                return resp;
            }
        }

        public class recentthreadsJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_recentthreads";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);

                try
                {
                    List<HomeThread> threads    = new List<HomeThread>();
                    var categories              = Plugin.Main.forum.getAllCategories();

                    foreach (var t in categories)
                    {
                        if ((t.permissions.Find(x => x.rankID == currentSession.sessionUser.ranking.id).actionable & Permission.Action.READ) != Permission.Action.READ)
                            continue;

                        HomeThread thread           = new HomeThread();
                        thread.categoryTitle        = t.title;
                        thread.categoryDescription  = t.description;
                        thread.categoryID           = t.id;
                        thread.recentThreads        = Plugin.Main.forum.getRecentThreads(t.id);

                        threads.Add(thread);
                    }

                    resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(threads);
                }
                catch (Exception e)
                {
                    //logme
                    resp.content = "";
                }

                return resp;
            }
        }

        public class categorythreadsJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_categorythreads";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
                var aResp = new ActionResponse();

                try
                {
                    var category = Plugin.Main.forum.getCategory(Convert.ToInt32(querySet["id"]));

                    if ((category.permissions.Find(x => x.rankID == currentSession.sessionUser.ranking.id).actionable & Permission.Action.READ) != Permission.Action.READ)
                        throw new Exceptions.PermissionException("User cannot view this category");

                    var categoryThreads = Plugin.Main.forum.getCategoryThreads(category.id);

                    resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(categoryThreads);
                    return resp;
                }
                
                catch (FormatException)
                {
                    //logme
                    aResp.errorCode = Manager.ErrorCode.CATEGORY_INVALID;
                }

                catch (Exceptions.CategoryException)
                {
                    //logme
                    aResp.errorCode = Manager.ErrorCode.CATEGORY_INVALID;
                }

                catch (Exceptions.PermissionException)
                {
                    aResp.errorCode = Manager.ErrorCode.GLOBAL_PERMISSIONDENIED;
                }

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(aResp);
                return resp;
            }
        }

        public class threadJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_thread";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
                var aResp = new ActionResponse();
                aResp.success = false;
                aResp.errorCode = Manager.ErrorCode.NO_ERROR;

                try
                {
                    if (querySet.Get("id") != null)
                    {
                        var thread = Plugin.Main.forum.getThread(Convert.ToInt32(querySet["id"]));

                        if ((thread.threadCategory.permissions.Find(x => x.rankID == currentSession.sessionUser.ranking.id).actionable & Permission.Action.READ) != Permission.Action.READ)
                            throw new Exceptions.PermissionException("User cannot view this post");

                        var replies = Plugin.Main.forum.getReplies(thread.id);

                        resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(new ThreadView(thread, replies));
                        aResp.success = true;
                    }

                    else if (querySet.Get("replyid") != null)
                    {
                        var thread = Plugin.Main.forum.getReply(Convert.ToInt32(querySet["replyid"]));

                        //if ((thread.threadCategory.permissions.Find(x => x.rankID == currentSession.sessionUser.ranking.id).actionable & Permission.Action.READ) != Permission.Action.READ)
                        //    throw new Exceptions.PermissionException("User cannot view this post");

                        resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(thread);
                        aResp.success = true;
                    }
                }

                catch (FormatException)
                {
                    aResp.errorCode = Manager.ErrorCode.THREAD_INVALID;
                }

                catch (Exceptions.ThreadException)
                {
                    aResp.errorCode = Manager.ErrorCode.THREAD_INVALID;
                }

                catch (Exceptions.PermissionException)
                {
                    aResp.errorCode = Manager.ErrorCode.GLOBAL_PERMISSIONDENIED;
                }

                if (aResp.success == false)
                    resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(aResp);

                return resp;
            }
        }

        public class editthreadJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_editthread";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
                var aResp = new ActionResponse();
                aResp.success = false;
                aResp.errorCode = Manager.ErrorCode.NO_ERROR;

                try
                {
                    if (querySet.Get("id") != null)
                    {
                        var thread = Plugin.Main.forum.getThread(Convert.ToInt32(querySet["id"]));

                        if (thread.author.id != currentSession.sessionUser.id && (thread.threadCategory.permissions.Find(x => x.rankID == currentSession.sessionUser.ranking.id).actionable & Permission.Action.MODIFY) != Permission.Action.MODIFY)
                            throw new Exceptions.PermissionException("User cannot modify this post");

                        if (querySet.Get("delete") != null)
                        {
                            thread.visible = false;
                            aResp.errorCode = Plugin.Main.forum.updateThread(thread);
                            aResp.success = aResp.errorCode == Manager.ErrorCode.NO_ERROR;
                            aResp.destination = "category?id=" + thread.threadCategory.id;
                        }

                        else if (querySet.Get("update") != null)
                        {
                            if (querySet.Get("content") == null || querySet.Get("title") == null)
                                throw new Exceptions.ThreadException("Invalid update data");

                            if (querySet.Get("content").Length > Manager.CONTENT_MAXLENGTH)
                            {
                                aResp.errorCode = Manager.ErrorCode.THREAD_CONTENTTOOLONG;
                            }

                            else if (querySet.Get("title").Length > Manager.TITLE_MAXLENGTH)
                            {
                                aResp.errorCode = Manager.ErrorCode.THREAD_TITLETOOLONG;
                            }

                            else
                            {
                                //fixsecurity
                                var fmtr = new DNA.Text.BBCodeFormatter();
                                string content = fmtr.Format(Uri.UnescapeDataString(querySet["content"]));
                                string title = DNA.Text.TextEngine.Text(Uri.UnescapeDataString(querySet["title"]));

                                if (thread.updateTitle(title) && thread.updateContent(content))
                                {
                                    aResp.errorCode = Plugin.Main.forum.updateThread(thread);
                                    aResp.success = aResp.errorCode == Manager.ErrorCode.NO_ERROR;
                                    aResp.destination = "thread?id=" + thread.id;
                                }
                                else
                                    aResp.errorCode = Manager.ErrorCode.THREAD_EMPTYDATA;
                            }
                        }
                    }

                    else if (querySet.Get("replyid") != null)
                    {
                        var reply = Plugin.Main.forum.getReply(Convert.ToInt32(querySet["replyid"]));
        
                        if (currentSession.sessionUser.id == 0 || reply.author.id != currentSession.sessionUser.id && (reply.threadCategory.permissions.Find(x => x.rankID == currentSession.sessionUser.ranking.id).actionable & Permission.Action.MODIFY) != Permission.Action.MODIFY)
                            throw new Exceptions.PermissionException("User cannot modify this reply");

                        if (querySet.Get("delete") != null)
                        {
                            reply.visible = false;
                            aResp.errorCode = Plugin.Main.forum.updateReply(reply);
                            aResp.success = aResp.errorCode == Manager.ErrorCode.NO_ERROR;
                            aResp.destination = "thread?id=" + reply.threadid;
                        }

                        else if (querySet.Get("content") != null)
                        {
                            if (querySet.Get("content") == null || querySet.Get("title") == null)
                                throw new Exceptions.ThreadException("Invalid update data");

                            if (querySet.Get("content").Length > Manager.CONTENT_MAXLENGTH)
                            {
                                aResp.errorCode = Manager.ErrorCode.THREAD_CONTENTTOOLONG;
                            }

                            else if (querySet.Get("title").Length > Manager.TITLE_MAXLENGTH)
                            {
                                aResp.errorCode = Manager.ErrorCode.THREAD_TITLETOOLONG;
                            }

                            else
                            {
                                int threadID = Convert.ToInt32(querySet["threadid"]);
                                //fixsecurity
                                var fmtr = new DNA.Text.BBCodeFormatter();
                                string content = fmtr.Format(Uri.UnescapeDataString(querySet["content"]));
                                string title = DNA.Text.TextEngine.Text(Uri.UnescapeDataString(querySet["title"]));

                                if (reply.updateTitle(title) && reply.updateContent(content))
                                {
                                    aResp.errorCode = Plugin.Main.forum.updateReply(reply);
                                    aResp.success = aResp.errorCode == Manager.ErrorCode.NO_ERROR;
                                    aResp.destination = "thread?id=" + threadID;
                                }
                                else
                                    aResp.errorCode = Manager.ErrorCode.THREAD_EMPTYDATA;
                            }
                        }

                    }

                    resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(aResp);
                }

                catch (FormatException)
                {
                    aResp.errorCode = Manager.ErrorCode.THREAD_INVALID;
                }

                catch (Exceptions.ThreadException)
                {
                    aResp.errorCode = Manager.ErrorCode.THREAD_INVALID;
                }

                catch (Exceptions.PermissionException)
                {
                    aResp.errorCode = Manager.ErrorCode.GLOBAL_PERMISSIONDENIED;
                }

                if (aResp.success == false)
                    resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(aResp);

                return resp;
            }
        }

        public class postthreadJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_postthread";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
                ActionResponse aResp = new ActionResponse();

                if (currentSession.sessionUser.ranking.equivalentRank < Player.Permission.Trusted)
                {
                    aResp.errorCode = Manager.ErrorCode.USER_NOTAUTHORIZED;
                }

                else
                {
                    try
                    {
                        if (querySet["content"].Length < Manager.CONTENT_MAXLENGTH && querySet["title"].Length <= Manager.TITLE_MAXLENGTH)
                        {

                            var fmtr = new DNA.Text.BBCodeFormatter();
                            string content = fmtr.Format(Uri.UnescapeDataString(querySet["content"]));
                            string title = DNA.Text.TextEngine.Text(Uri.UnescapeDataString(querySet["title"]));

                            if (querySet.Get("threadid") != null)
                            {
                                var replyThread = Plugin.Main.forum.getThread(Convert.ToInt32(querySet.Get("threadid")));
                                var reply = new Post(title, replyThread.getID(), content, currentSession.sessionUser);

                                aResp.errorCode = Plugin.Main.forum.addPost(replyThread, reply);
                                aResp.destination = String.Format("thread?id={0}", replyThread.id);
                                aResp.success = aResp.errorCode == Manager.ErrorCode.NO_ERROR;
                            }

                            else
                            {
                                Category threadCategory = Plugin.Main.forum.getCategory(Convert.ToInt32(querySet["category"]));

                                if ((threadCategory.permissions.Find(x => x.rankID == currentSession.sessionUser.ranking.id).actionable & Permission.Action.WRITE) == Permission.Action.WRITE)
                                {
                                    ForumThread newThread = new ForumThread(title, content, currentSession.sessionUser, threadCategory);

                                    aResp.errorCode = Plugin.Main.forum.addThread(newThread);
                                    aResp.destination = String.Format("category?id={0}", threadCategory.id);
                                    aResp.success = aResp.errorCode == Manager.ErrorCode.NO_ERROR;
                                }

                                else
                                    aResp.errorCode = Manager.ErrorCode.USER_NOTAUTHORIZED;
                            }
                        }

                        else if (querySet["title"].Length > Manager.TITLE_MAXLENGTH)
                            aResp.errorCode = Manager.ErrorCode.THREAD_TITLETOOLONG;
                        else
                            aResp.errorCode = Manager.ErrorCode.THREAD_CONTENTTOOLONG;
                    }

                    catch (Exceptions.ThreadException)
                    {
                        aResp.errorCode = Manager.ErrorCode.THREAD_BADDATA;
                    }

                    catch (NullReferenceException)
                    {
                        //logme
                        aResp.errorCode = Manager.ErrorCode.THREAD_EMPTYDATA;
                    }
                }

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(aResp);
                return resp;
            }
        }

        public class StatsJSON : JSONPage
        {
            public override string getPath()
            {
                return base.getPath() + "/_stats";
            }

            public override HttpResponse getPage(NameValueCollection querySet, IDictionary<string, string> requestHeaders)
            {
                var resp = base.getPage(querySet, requestHeaders);
                StatView stats = new StatView();

                stats.onlineUsers = new List<User>();

                foreach (Session s in Plugin.Main.forum.getSessions())
                {
                    if (s.sessionUser.ranking.id > 0 && (DateTime.Now - s.sessionStartTime).TotalMinutes < 5 && s.sessionUser.username != "Guest")
                        stats.onlineUsers.Add(s.sessionUser);
                }
                stats.onlineUsers.OrderByDescending(x => x.ranking.equivalentRank);

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(stats);
                return resp;
            }
        }


        protected struct StatView
        {
            public List<User> onlineUsers;
        }

        protected struct ActionResponse
        {
            public bool success;
            public string destination;
            public Manager.ErrorCode errorCode;
        }

        protected struct HomeThread
        {
            public string categoryTitle;
            public string categoryDescription;
            public int categoryID;
            public List<ForumThread> recentThreads;
        }

        protected struct ThreadView
        {
            public ForumThread Thread;
            public List<Post> Replies;

            public ThreadView(ForumThread t, List<Post> r)
            {
                Thread = t;
                Replies = r;
            }
        }
    }
}