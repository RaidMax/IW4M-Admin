using System;

namespace SharedLibraryCore.Exceptions
{
    public class RConException : Exception
    {
        public bool IsOperationCancelled { get; }
        
        public RConException(string message) : base(message)
        {
        }
        
        public RConException(string message, bool isOperationCancelled) : base(message)
        {
            IsOperationCancelled = isOperationCancelled;
        }
    }
}
