namespace SharedLibraryCore.Commands
{
    public static class CommandExtensions
    {
        public static bool IsTargetingSelf(this GameEvent gameEvent)
        {
            return gameEvent.Origin?.Equals(gameEvent.Target) ?? false;
        }

        public static bool CanPerformActionOnTarget(this GameEvent gameEvent)
        {
            return gameEvent.Origin?.Level > gameEvent.Target?.Level;
        }
    }
}