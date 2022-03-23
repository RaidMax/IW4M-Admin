using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands.ClientTags
{
    public class SetClientTagCommand : Command
    {
        private readonly IMetaServiceV2 _metaService;


        public SetClientTagCommand(CommandConfiguration config, ITranslationLookup layout, IMetaServiceV2 metaService) :
            base(config, layout)
        {
            Name = "setclienttag";
            Description = layout["COMMANDS_SET_CLIENT_TAG_DESC"];
            Alias = "sct";
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
            var token = gameEvent.Owner.Manager.CancellationToken;
            
            var availableTags = await _metaService.GetPersistentMetaValue<List<LookupValue<string>>>(EFMeta.ClientTagNameV2, token);
            var matchingTag = availableTags.FirstOrDefault(tag => tag.Value == gameEvent.Data.Trim());

            if (matchingTag == null)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SET_CLIENT_TAG_FAIL"].FormatExt(gameEvent.Data));
                return;
            }

            gameEvent.Target.Tag = matchingTag.Value;
            await _metaService.SetPersistentMetaForLookupKey(EFMeta.ClientTagV2, EFMeta.ClientTagNameV2, matchingTag.Id,
                gameEvent.Target.ClientId, token);
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_SET_CLIENT_TAG_SUCCESS"].FormatExt(matchingTag.Value));
        }
    }
}
