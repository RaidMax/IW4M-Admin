using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageBoard.Exceptions
{
   public class ThreadException : Exception
   {
       public ThreadException(string msg) : base(msg) { }
   }

   public class UserException : Exception
   {
       public UserException(string msg) : base(msg) { }
   }

    public class SessionException : Exception
    {
        public SessionException(string msg) : base(msg) { }
    }

    public class CategoryException : Exception
    {
        public CategoryException(string msg) : base(msg) { }
    }

    public class PermissionException: Exception
    {
        public PermissionException(string msg) : base(msg) { }
    }
}
    