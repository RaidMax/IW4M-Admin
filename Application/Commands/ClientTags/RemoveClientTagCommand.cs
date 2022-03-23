using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Data.Models;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands.ClientTags
{
    public class RemoveClientTag : Command
    {
        private readonly IMetaServiceV2 _metaService;

        public RemoveClientTag(CommandConfiguration config, ITranslationLookup layout, IMetaServiceV2 metaService) : base(
            config, layout)
        {
            Name = "removeclienttag";
            Description = layout["COMMANDS_REMOVE_CLIENT_TAG_DESC"];
            Alias = "rct";
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
            var existingMeta = await _metaService.GetPersistentMetaValue<List<TagMeta>>(EFMeta.ClientTagNameV2,
                gameEvent.Owner.Manager.CancellationToken);
            existingMeta = existingMeta.Where(meta => meta.TagName != gameEvent.Data.Trim()).ToList();
            await _metaService.SetPersistentMetaValue(EFMeta.ClientTagNameV2, existingMeta,
                gameEvent.Owner.Manager.CancellationToken);
            
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_REMOVE_CLIENT_TAG_SUCCESS"].FormatExt(gameEvent.Data));
        }
    }
}
