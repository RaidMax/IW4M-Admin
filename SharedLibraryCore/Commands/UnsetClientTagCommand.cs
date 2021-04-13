using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using Data.Models;

namespace SharedLibraryCore.Commands
{
    public class UnsetClientTagCommand : Command
    {
        private readonly IMetaService _metaService;


        public UnsetClientTagCommand(CommandConfiguration config, ITranslationLookup layout, IMetaService metaService) : base(config, layout)
        {
            Name = "unsetclienttag";
            Description = layout["COMMANDS_UNSET_CLIENT_TAG_DESC"];
            Alias = "uct";
            Permission = EFClient.Permission.Owner;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGUMENT_TAG"],
                    Required = true
                }
            };

            _metaService = metaService;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            gameEvent.Target.Tag = null;
            await _metaService.RemovePersistentMeta(EFMeta.ClientTag, gameEvent.Target);
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_UNSET_CLIENT_TAG_SUCCESS"]);
        }
    }
}
