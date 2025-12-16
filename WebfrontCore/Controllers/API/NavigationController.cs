using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;
using Page = WebfrontCore.Core.Services.Page;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class NavigationController(
        IManager manager,
        IInteractionRegistration interactionRegistration)
        : BaseController(manager)
    {
        [HttpGet]
        public async Task<ActionResult<NavigationData>> GetNavigationData()
        {
            // Get pages from Manager's page list (IDictionary<string, string> where key=name, value=location)
            var rawPages = Manager.GetPageList().Pages;
            var pages = rawPages
                .Select(kvp => new Page { Name = kvp.Key, Location = kvp.Value })
                .ToList();

            // Get all navigation interactions (Main, Admin, Social)
            var interactions = (await interactionRegistration.GetInteractions("Webfront::Nav"))
                .Select(i => new InteractionData
                {
                    InteractionId = i.InteractionId,
                    MinimumPermission = (int)(i.MinimumPermission ?? Data.Models.Client.EFClient.Permission.User),
                    Name = i.Name,
                    DisplayMeta = i.DisplayMeta
                })
                .ToList();

            ClientInfo user = null;
            if (Authorized && Client != null)
            {
                user = new ClientInfo
                {
                    ClientId = Client.ClientId,
                    Name = Client.CurrentAlias?.Name ?? "Unknown",
                    Level = Client.Level,
                    Game = Client.GameName
                };
            }

            // Get localization dictionary from TranslationLookup.Set
            var localization = Utilities.CurrentLocalization.LocalizationIndex.Set;

            return new NavigationData
            {
                User = user,
                Authorized = Authorized,
                Localization = localization,
                Pages = pages,
                Interactions = interactions,
                CommunityInformation = new CommunityInformation
                {
                    IsEnabled = Manager.GetApplicationSettings().Configuration().CommunityInformation?.IsEnabled ?? false,
                    SocialAccounts = Manager.GetApplicationSettings().Configuration().CommunityInformation?.SocialAccounts?
                        .Select(s => new SocialAccountConfiguration
                        {
                            Title = s.Title,
                            Url = s.Url,
                            IconId = s.IconId,
                            IconUrl = s.IconUrl
                        }).ToArray() ?? System.Array.Empty<SocialAccountConfiguration>()
                },
                TotalClientCount = Manager.GetServers().Sum(server => server.ClientNum),
                TotalAdminCount = Manager.GetServers().Sum(server =>
                    server.GetClientsAsList()
                        .Count(client => client.Level >= Data.Models.Client.EFClient.Permission.Trusted)),
                TotalReportCount = Manager.GetServers().Sum(server =>
                    server.Reports.Count(report => System.DateTime.UtcNow - report.ReportedOn <= System.TimeSpan.FromHours(24)))
            };
        }
    }
}
