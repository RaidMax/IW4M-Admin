using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands.ClientTags
{
    public class AddClientTagCommand : Command
    {
        private readonly IMetaServiceV2 _metaService;

        public AddClientTagCommand(ILogger<AddClientTagCommand> commandLogger, CommandConfiguration config,
            ITranslationLookup layout, IMetaServiceV2 metaService) :
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
            logger = commandLogger;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var existingTags = await _metaService.GetPersistentMetaValue<List<TagMeta>>(EFMeta.ClientTagNameV2) ??
                               new List<TagMeta>();

            var tagName = gameEvent.Data.Trim();

            if (existingTags.Any(tag => tag.TagName == tagName))
            {
                logger.LogWarning("Tag with name {TagName} already exists", tagName);
                return;
            }

            existingTags.Add(new TagMeta
            {
                Id = (existingTags.LastOrDefault()?.TagId ?? 0) + 1,
                Value = tagName
            });

            await _metaService.SetPersistentMetaValue(EFMeta.ClientTagNameV2, existingTags,
                gameEvent.Owner.Manager.CancellationToken);
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_ADD_CLIENT_TAG_SUCCESS"].FormatExt(gameEvent.Data));
        }
    }
}
