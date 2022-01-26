namespace SharedLibraryCore.Exceptions
{
    public class CommandException : ServerException
    {
        public CommandException(string msg) : base(msg)
        {
        }

        // .data contains
        // "command_name"
    }
}