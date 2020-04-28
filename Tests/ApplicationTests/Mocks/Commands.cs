using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Threading.Tasks;

namespace ApplicationTests.Mocks
{
    class ImpersonatableCommand : Command
    {
        public ImpersonatableCommand(CommandConfiguration config, ITranslationLookup lookup) : base(config, lookup)
        {
            AllowImpersonation = true;
            Name = nameof(ImpersonatableCommand);
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            E.Origin.Tell("test");
            return Task.CompletedTask;
        }
    }

    class NonImpersonatableCommand : Command
    {
        public NonImpersonatableCommand(CommandConfiguration config, ITranslationLookup lookup) : base(config, lookup)
        {
            Name = nameof(NonImpersonatableCommand);
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            return Task.CompletedTask;
        }
    }
}
