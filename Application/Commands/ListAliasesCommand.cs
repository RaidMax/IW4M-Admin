using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Lists alises of specified client
    /// </summary>
    public class ListAliasesCommand : Command
    {
        public ListAliasesCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "alias";
            Description = _translationLookup["COMMANDS_ALIAS_DESC"];
            Alias = "known";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true,
                }
            };
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            var message = new StringBuilder();
            var names = new List<string>(gameEvent.Target.AliasLink.Children.Select(a => a.Name));
            var ips = new List<string>(gameEvent.Target.AliasLink.Children.Select(a => a.IPAddress.ConvertIPtoString())
                .Distinct());

            gameEvent.Origin.Tell($"[(Color::Accent){gameEvent.Target}(Color::White)]");

            message.Append($"{_translationLookup["COMMANDS_ALIAS_ALIASES"]}: ");
            message.Append(string.Join(" | ", names));
            gameEvent.Origin.Tell(message.ToString());

            message.Clear();
            message.Append($"{_translationLookup["COMMANDS_ALIAS_IPS"]}: ");
            message.Append(string.Join(" | ", ips));
            gameEvent.Origin.Tell(message.ToString());

            return Task.CompletedTask;
        }
    }
}