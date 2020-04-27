using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Commands
{
    public static class CommandExtensions
    {
        public static bool IsTargetingSelf(this GameEvent gameEvent) => gameEvent.Origin?.Equals(gameEvent.Target) ?? false;

        public static bool CanPerformActionOnTarget(this GameEvent gameEvent) => gameEvent.Origin?.Level > gameEvent.Target?.Level;
    }
}
