using FluentValidation;
using System.Net;

namespace SharedLibraryCore.Configuration.Validation
{
    /// <summary>
    /// Validation class for server configuration
    /// </summary>
    public class ServerConfigurationValidator : AbstractValidator<ServerConfiguration>
    {
        public ServerConfigurationValidator()
        {
            RuleFor(_server => _server.IPAddress)
                .NotEmpty()
                .Must(_address => IPAddress.TryParse(_address, out _));

            RuleFor(_server => _server.Port)
                .InclusiveBetween(1, ushort.MaxValue);

            RuleFor(_server => _server.Password)
                .NotEmpty();

            RuleForEach(_server => _server.Rules)
                .NotEmpty();

            RuleForEach(_server => _server.AutoMessages)
                .NotEmpty();

            RuleFor(_server => _server.ReservedSlotNumber)
                .InclusiveBetween(0, 32);

            RuleFor(_server => _server.CustomHostname)
                .MinimumLength(3)
                .MaximumLength(128);
        }
    }
}
