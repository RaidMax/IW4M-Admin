using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Extensions;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Controllers
{
    public class AboutController : BaseController
    {
        private readonly ApplicationConfiguration _appConfig;

        public AboutController(IManager manager, ApplicationConfiguration appConfig) : base(manager)
        {
            _appConfig = appConfig;
        }

        public IActionResult Index()
        {
            ViewBag.Description = Localization["WEBFRONT_DESCRIPTION_ABOUT"].FormatExt(
                _appConfig.ShouldUseFallbackBranding()
                    ? _appConfig.WebfrontCustomBranding
                    : _appConfig.CommunityInformation.Name);
            ViewBag.Title = _appConfig.ShouldUseFallbackBranding()
                ? Localization["WEBFRONT_NAV_ABOUT"]
                : _appConfig.CommunityInformation.Name;

            var info = new CommunityInfo
            {
                GlobalRules = _appConfig.GlobalRules,
                ServerRules =
                    _appConfig.Servers.ToDictionary(
                        config => Manager.GetServers().FirstOrDefault(server =>
                            server.IP == config.IPAddress && server.Port == config.Port)?.Hostname,
                        config => config.Rules),
                CommunityInformation = _appConfig.CommunityInformation
            };

            return View(info);
        }
    }
}