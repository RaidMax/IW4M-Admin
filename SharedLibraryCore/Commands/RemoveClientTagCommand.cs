using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;

namespace SharedLibraryCore.Commands
{
    public class RemoveClientTag : Command
    {
        private readonly IMetaService _metaService;

        public RemoveClientTag(CommandConfiguration config, ITranslationLookup layout, IMetaService metaService) : base(config, layout)
        {
            Name = "removeclienttag";
            Description = layout["COMMANDS_REMOVE_CLIENT_TAG_DESC"];
            Alias = "rct";
            Permission = EFClient.Permission.Owner;
            RequiresTarget = false;
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
            await _metaService.RemovePersistentMeta(EFMeta.ClientTagName, gameEvent.Data);
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_REMOVE_CLIENT_TAG_SUCCESS"].FormatExt(gameEvent.Data));
        }
    }
}
