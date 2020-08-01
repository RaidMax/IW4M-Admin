using SharedLibraryCore;
using System;


namespace ApplicationTests.Fixtures
{
    static class EventGenerators
    {
        public static GameEvent GenerateEvent(GameEvent.EventType type, string data, Server owner)
        {
            switch (type)
            {
                case GameEvent.EventType.Command:
                    return new GameEvent
                    {
                        Origin = ClientGenerators.CreateDatabaseClient(),
                        Data = data,
                        Message = data,
                        Owner = owner
                    };
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
