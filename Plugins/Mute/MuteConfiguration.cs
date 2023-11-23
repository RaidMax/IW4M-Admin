using SharedLibraryCore;

namespace IW4MAdmin.Plugins.Mute;

public class MuteConfiguration
{
    public string? GetCommand(Server.Game game, MuteAction action) => GameCommands
        .FirstOrDefault(x => x.GameName == game)?.Commands
        .FirstOrDefault(x => x.Name == action)?.Action;

    public List<GameCommand> GameCommands { get; set; } = new()
    {
        new GameCommand
        {
            GameName = Server.Game.IW4,
            Commands = new List<Command>
            {
                new()
                {
                    Name = MuteAction.Mute,
                    Action = "muteClient"
                },
                new()
                {
                    Name = MuteAction.Unmute,
                    Action = "unmute"
                }
            }
        },
        new GameCommand
        {
            GameName = Server.Game.IW6,
            Commands = new List<Command>
            {
                new()
                {
                    Name = MuteAction.Mute,
                    Action = "muteClient"
                },
                new()
                {
                    Name = MuteAction.Unmute,
                    Action = "unmuteClient"
                }
            }
        },
    };

    public class GameCommand
    {
        public Server.Game GameName { get; set; }
        public List<Command> Commands { get; set; }
    }

    public class Command
    {
        public MuteAction Name { get; set; }
        public string Action { get; set; }
    }
}

public enum MuteAction
{
    Mute,
    Unmute
}
