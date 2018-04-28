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

        public Task OnEventAsync(GameEvent E, Server S) => Task.CompletedTask;

        public Task OnLoadAsync(IManager manager) => Task.CompletedTask;

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public Task OnUnloadAsync() => Task.CompletedTask;
    }
}
