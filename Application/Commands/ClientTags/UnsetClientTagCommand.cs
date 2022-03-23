using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands.ClientTags
{
    public class UnsetClientTagCommand : Command
    {
        private readonly IMetaServiceV2 _metaService;

        public UnsetClientTagCommand(CommandConfiguration config, ITranslationLookup layout, IMetaServiceV2 metaService) :
            base(config, layout)
        {
            Name = "unsetclienttag";
            Description = layout["COMMANDS_UNSET_CLIENT_TAG_DESC"];
            Alias = "uct";
            Permission = EFClient.Permission.Owner;
            RequiresTarget = true;
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
            gameEvent.Target.Tag = null;
            await _metaService.RemovePersistentMeta(EFMeta.ClientTagV2, gameEvent.Target.ClientId,
                gameEvent.Owner.Manager.CancellationToken);
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_UNSET_CLIENT_TAG_SUCCESS"]);
        }
    }
}
