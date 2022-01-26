using System.Linq;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Commands
{
    public class ListClientTags : Command
    {
        private readonly IMetaService _metaService;

        public ListClientTags(CommandConfiguration config, ITranslationLookup layout, IMetaService metaService) : base(
            config, layout)
        {
            Name = "listclienttags";
            Description = layout["COMMANDS_LIST_CLIENT_TAGS_DESC"];
            Alias = "lct";
            Permission = EFClient.Permission.Owner;
            RequiresTarget = false;

            _metaService = metaService;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            var tags = await _metaService.GetPersistentMeta(EFMeta.ClientTagName);
            gameEvent.Origin.Tell(tags.Select(tag => tag.Value));
        }
    }
}