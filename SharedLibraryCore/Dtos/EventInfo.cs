using System;

namespace SharedLibraryCore.Dtos
{
    public class EventInfo
    {
        public EventInfo(EventType Ty, EventVersion V, string M, string T, string O, string Ta)
        {
            Type = Ty;
            Version = V;
            Message = System.Web.HttpUtility.HtmlEncode(M);
            Title = T;
            Origin = System.Web.HttpUtility.HtmlEncode(O);
            Target = System.Web.HttpUtility.HtmlEncode(Ta);

            ID = Math.Abs(DateTime.Now.GetHashCode());
        }

        public enum EventType
        {
            NOTIFICATION,
            STATUS,
            ALERT,
        }

        public enum EventVersion
        {
            IW4MAdmin
        }

        public EventType Type;
        public EventVersion Version;
        public string Message;
        public string Title;
        public string Origin;
        public string Target;
        public int ID;
    }
}