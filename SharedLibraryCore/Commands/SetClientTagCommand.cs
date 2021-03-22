using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using Data.Models;

namespace SharedLibraryCore.Commands
{
    public class SetClientTagCommand : Command
    {
        private readonly IMetaService _metaService;


        public SetClientTagCommand(CommandConfiguration config, ITranslationLookup layout, IMetaService metaService) : base(config, layout)
        {
            Name = "setclienttag";
            Description = layout["COMMANDS_SET_CLIENT_TAG_DESC"];
            Alias = "sct";
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
            var availableTags = await _metaService.GetPersistentMeta(EFMeta.ClientTagName);
            var matchingTag = availableTags.FirstOrDefault(tag => tag.Value == gameEvent.Data);

            if (matchingTag == null)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_SET_CLIENT_TAG_FAIL"].FormatExt(gameEvent.Data));
                return;
            }

            gameEvent.Target.Tag = matchingTag.Value;
            await _metaService.AddPersistentMeta(EFMeta.ClientTag, string.Empty, gameEvent.Target, matchingTag);
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_SET_CLIENT_TAG_SUCCESS"].FormatExt(matchingTag.Value));
        }
    }
}
