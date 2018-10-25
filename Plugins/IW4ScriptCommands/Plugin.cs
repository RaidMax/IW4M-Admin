using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IW4ScriptCommands
{
    class Plugin : IPlugin
    {
        public string Name => "IW4 Script Commands";

        public float Version => 1.0f;

        public string Author => "RaidMax";

        public Task OnEventAsync(GameEvent E, Server S)
        {
            if (E.Type == GameEvent.EventType.Start)
            {
                return S.SetDvarAsync("sv_iw4madmin_serverid", S.GetHashCode());
            }

            if (E.Type == GameEvent.EventType.Warn)
            {
                return S.SetDvarAsync("sv_iw4madmin_command", new CommandInfo()
                {
                    ClientNumber = E.Target.ClientNumber,
                    Command = "alert",
                    CommandArguments = new List<string>()
                    {
                        "Warning",
                        "ui_mp_nukebomb_timer",
                        E.Data
                    }
                }.ToString());
            }

            return Task.CompletedTask;
        }

        public Task OnLoadAsync(IManager manager) => Task.CompletedTask;

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public Task OnUnloadAsync() => Task.CompletedTask;
    }
}
