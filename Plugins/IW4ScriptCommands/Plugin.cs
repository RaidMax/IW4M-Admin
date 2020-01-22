using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;

namespace IW4ScriptCommands
{
    public class Plugin : IPlugin
    {
        public string Name => "IW4 Script Commands";

        public float Version => 1.0f;

        public string Author => "RaidMax";

        public async Task OnEventAsync(GameEvent E, Server S)
        {
            if (E.Type == GameEvent.EventType.Start)
            {
                await S.SetDvarAsync("sv_iw4madmin_serverid", S.EndPoint);
            }

            if (E.Type == GameEvent.EventType.Warn)
            {
                var cmd = new ScriptCommand()
                {
                    ClientNumber = E.Target.ClientNumber,
                    CommandName = "alert",
                    CommandArguments = new[]
                    {
                        "Warning",
                        "ui_mp_nukebomb_timer",
                        E.Data
                    }
                };
                // notifies the player ingame of the warning
                await cmd.Execute(S);
            }
        }

        public Task OnLoadAsync(IManager manager) => Task.CompletedTask;

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public Task OnUnloadAsync() => Task.CompletedTask;
    }
}
