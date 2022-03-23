using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands.ClientTags
{
    public class ListClientTags : Command
    {
        private readonly IMetaServiceV2 _metaService;

        public ListClientTags(CommandConfiguration config, ITranslationLookup layout, IMetaServiceV2 metaService) : base(
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
            var tags = await _metaService.GetPersistentMetaValue<List<TagMeta>>(EFMeta.ClientTagNameV2);
            
            if (tags is not null)
            {
                gameEvent.Origin.Tell(tags.Select(tag => tag.TagName));
            }
        }
    }
}
