using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Commands
{
    public class AddClientTagCommand : Command
    {
        private readonly IMetaService _metaService;

        public AddClientTagCommand(CommandConfiguration config, ITranslationLookup layout, IMetaService metaService) :
            base(config, layout)
        {
            Name = "addclienttag";
            Description = layout["COMMANDS_ADD_CLIENT_TAG_DESC"];
            Alias = "act";
            Permission = EFClient.Permission.Owner;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGUMENT_TAG"],
                    Required = true
                }
            };

            _metaService = metaService;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            await _metaService.AddPersistentMeta(EFMeta.ClientTagName, gameEvent.Data);
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_ADD_CLIENT_TAG_SUCCESS"].FormatExt(gameEvent.Data));
        }
    }
}