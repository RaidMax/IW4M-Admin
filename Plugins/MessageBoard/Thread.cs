using System;
using System.Collections.Generic;

namespace MessageBoard
{
    
    public class Post : ForumThread
    {
        /// <summary>
        /// Initial creation
        /// </summary>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <param name="author"></param>
        /// <param name="parentThread"></param>
        /// 

        public int threadid; 

        public Post(string title, int threadid, string content, User author) : base (title, content, author, null)
        {
            this.threadid = threadid;
        }

        public Post(int id, int threadid, bool visible, string title, string content, User author, DateTime creationDate, DateTime updatedDate) : base(id, title, visible, content, 0, author, null, creationDate, updatedDate)
        {
            this.lastModificationString = SharedLibrary.Utilities.timePassed(creationDate);
            this.threadid = threadid;
        }
        
    }

    
    public class Category : Identifiable
    {
        public int id { get; private set; }
        public string title { get; private set; }
        public string description { get; private set; }
        public List<Permission> permissions { get; private set; }

        public Category(string title, string description)
        {
            this.title = title;
            this.description = description;
            this.permissions = new List<Permission>();
            id = 0;
        }

        public Category(int id, string title, string description, List<Permission> permissions)
        {
            this.title = title;
            this.description = description;
            this.id = id;
            this.permissions = permissions;
        }

        public int getID()
        {
            return id;
        }
    }

    public class ForumThread : Identifiable
    {
        public string title { get; private set; }
        public string content { get; private set; }
        public string formattedContent { get; private set; }
        public User author { get; private set; }
        public Category threadCategory { get; private set; }
        public DateTime creationDate { get; private set; }
        public DateTime updatedDate;
        public string lastModificationString { get; protected set;  }
        public int id { get; private set; }
        public int replies;
        public bool visible = true;

        /// <summary>
        /// Initial creation
        /// </summary>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <param name="author"></param>
        public ForumThread(string title, string content, User author, Category threadCategory)
        {
            if (content.Length == 0)
                throw new Exceptions.ThreadException("Post is empty");
            if (author == null)
                throw new Exceptions.ThreadException("No author of post");
            if (title.Length == 0)
                throw new Exceptions.ThreadException("Title is empty");

            this.title = title;
            this.content = content;  
            this.author = author;
            this.threadCategory = threadCategory;
            creationDate = DateTime.Now;
            updatedDate = DateTime.Now;
            replies = 0;
            id = 0;
        }

        /// <summary>
        /// Loading from database
        /// </summary>
        /// <param name="id"></param>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <param name="author"></param>
        /// <param name="creationDate"></param>
        public ForumThread(int id, string title, bool visible, string content, int replies, User author, Category threadCategory, DateTime creationDate, DateTime updatedDate)
        {
            this.id = id;
            this.replies = replies;
            this.title = title;
            this.content = content;
            this.formattedContent = CodeKicker.BBCode.BBCode.ToHtml(this.content);
            this.author = author;
            this.threadCategory = threadCategory;
            this.creationDate = creationDate;
            this.updatedDate = updatedDate;
            this.lastModificationString = SharedLibrary.Utilities.timePassed(updatedDate);
            this.visible = visible;
        }

        public int getID()
        {
            return id;
        }

        public bool updateContent(string content)
        {
            if (content != null && content.Length > 0)
            {
                this.content = content;
                return true;
            }

            return false;
        }

        public bool updateTitle(string title)
        {
            if (title != null && title.Length > 0)
            {
                this.title = title;
                return true;
            }

            return false;
        }
    }
}
