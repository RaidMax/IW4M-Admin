using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharedLibrary;

namespace MessageBoard
{
    public class User : Identifiable
    {
        private string passwordHash; // byte array -> b64 string
        private string passwordSalt; // byte array -> b64 string
        public DateTime lastLogin;
        public string lastLoginString;
        public readonly DateTime creationDate;
        public int id { get; private set; }
        public string avatarURL;

        public string username { get; private set; }
        public string email { get; private set; }
        public Rank ranking;

        public int posts;
        public int privateMessages;
        public int warnings;

        public List<int> subscribedThreads { get; private set; }

        public User()
        {
            username = "Guest";
            ranking = Plugin.Main.forum.guestRank;
        }

        /// <summary>
        /// When creating a new user
        /// </summary>
        /// <param name="username"></param>
        /// <param name="email"></param>
        /// <param name="passwordHash"></param>
        /// <param name="passwordSalt"></param>
        /// <param name="posts"></param>
        /// <param name="privateMessage"></param>
        /// <param name="warnings"></param>
        public User(string username, string matchedUsername, string email, string passwordHash, string passwordSalt, Rank ranking)
        {
            if (username.Length < 1)
                throw new Exceptions.UserException("Username is empty");
            if (email.Length < 1)
                throw new Exceptions.UserException("Email is empty");

            lastLogin = DateTime.Now;
            subscribedThreads = new List<int>();

            this.username = username;
            this.email = email;
            this.posts = 0;
            this.privateMessages = 0;
            this.warnings = 0;
            this.ranking = ranking;
            this.passwordHash = passwordHash;
            this.passwordSalt = passwordSalt;
            this.creationDate = DateTime.Now;
            this.avatarURL    = "";

            id = 0;
        }

        public User(int id, string passwordHash, string passwordSalt, string username, string email, int posts, DateTime lastLogin, DateTime creationDate, Rank ranking, string avatarURL)
        {
            this.id = id;
            this.passwordHash = passwordHash;
            this.passwordSalt = passwordSalt;
            this.username = username;
            this.email = email;
            this.lastLogin = lastLogin;
            this.creationDate = creationDate;
            this.ranking = ranking;
            this.avatarURL = avatarURL;
            this.posts = posts;

            this.lastLoginString = SharedLibrary.Utilities.timePassed(lastLogin);
        }

        public int getID()
        {
            return this.id;
        }

        public string getPasswordSalt()
        {
            return this.passwordSalt;
        }

        public string getPasswordHash()
        {
            return this.passwordHash;
        }
    }

    public struct UserInfo
    {
        public string username;
        public string email;
        public string matchedUsername;
        public Rank rank;
    }
}
