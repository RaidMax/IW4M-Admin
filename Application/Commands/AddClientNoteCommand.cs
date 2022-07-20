using System;
using System.Threading.Tasks;
using Data.Models.Client;
using IW4MAdmin.Application.Meta;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands;

public class AddClientNoteCommand : Command
{
    private readonly IMetaServiceV2 _metaService;

    public AddClientNoteCommand(CommandConfiguration config, ITranslationLookup layout, IMetaServiceV2 metaService) : base(config, layout)
    {
        Name = "addnote";
        Description = _translationLookup["COMMANDS_ADD_CLIENT_NOTE_DESCRIPTION"];
        Alias = "an";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = true;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                Required = true
            },
            new CommandArgument
            {
                Name = _translationLookup["COMMANDS_ARGS_NOTE"],
                Required = false
            }
        };
        
        _metaService = metaService;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var note = new ClientNoteMetaResponse
        {
            Note = gameEvent.Data?.Trim(),
            OriginEntityId = gameEvent.Origin.ClientId,
            ModifiedDate = DateTime.UtcNow
        };
        await _metaService.SetPersistentMetaValue("ClientNotes", note, gameEvent.Target.ClientId);
        gameEvent.Origin.Tell(_translationLookup["COMMANDS_ADD_CLIENT_NOTE_SUCCESS"]);
    }
}
