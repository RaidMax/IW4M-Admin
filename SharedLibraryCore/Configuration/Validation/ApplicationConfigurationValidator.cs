using System;
using System.Linq;
using System.Net;
using FluentValidation;

namespace SharedLibraryCore.Configuration.Validation
{
    /// <summary>
    ///     Validation class for main application configuration
    /// </summary>
    public class ApplicationConfigurationValidator : AbstractValidator<ApplicationConfiguration>
    {
        public ApplicationConfigurationValidator()
        {
            RuleFor(_app => _app.WebfrontBindUrl)
                .NotEmpty();

            RuleFor(_app => _app.CustomSayName)
                .NotEmpty()
                .When(_app => _app.EnableCustomSayName);

            RuleFor(_app => _app.SocialLinkAddress)
                .NotEmpty()
                .When(_app => _app.EnableSocialLink);

            RuleFor(_app => _app.SocialLinkTitle)
                .NotEmpty()
                .When(_app => _app.EnableSocialLink);

            RuleFor(_app => _app.CustomParserEncoding)
                .NotEmpty()
                .When(_app => _app.EnableCustomParserEncoding);

            RuleFor(_app => _app.WebfrontConnectionWhitelist)
                .NotEmpty()
                .When(_app => _app.EnableWebfrontConnectionWhitelist);

            RuleForEach(_app => _app.WebfrontConnectionWhitelist)
                .Must(_address => IPAddress.TryParse(_address, out _));

            RuleFor(_app => _app.CustomLocale)
                .NotEmpty()
                .When(_app => _app.EnableCustomLocale);

            RuleFor(_app => _app.DatabaseProvider)
                .NotEmpty()
                .Must(_provider => new[] { "sqlite", "mysql", "postgresql" }.Contains(_provider));

            RuleFor(_app => _app.ConnectionString)
                .NotEmpty()
                .When(_app => _app.DatabaseProvider != "sqlite");

            RuleFor(_app => _app.RConPollRate)
                .GreaterThanOrEqualTo(1000);

            RuleFor(_app => _app.AutoMessagePeriod)
                .GreaterThanOrEqualTo(60);

            RuleFor(_app => _app.Servers)
                .NotEmpty();

            RuleFor(_app => _app.AutoMessages)
                .NotNull();

            RuleFor(_app => _app.GlobalRules)
                .NotNull();

            RuleFor(_app => _app.MasterUrl)
                .NotNull()
                .Must(_url => _url != null && _url.Scheme == Uri.UriSchemeHttp);

            RuleFor(_app => _app.CommandPrefix)
                .NotEmpty();

            RuleFor(_app => _app.BroadcastCommandPrefix)
                .NotEmpty();

            RuleForEach(_app => _app.Servers)
                .NotEmpty()
                .SetValidator(new ServerConfigurationValidator());
        }
    }
}