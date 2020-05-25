using FluentValidation;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;

namespace WebfrontCore.Controllers.API.Validation
{
    /// <summary>
    /// validator for FindClientRequest
    /// </summary>
    public class FindClientRequestValidator : AbstractValidator<FindClientRequest>
    {
        public FindClientRequestValidator()
        {
            RuleFor(_request => _request.Name)
                .NotEmpty()
                .When(_request => string.IsNullOrEmpty(_request.Xuid));

            RuleFor(_request => _request.Name)
                .MinimumLength(EFAlias.MIN_NAME_LENGTH)
                .MaximumLength(EFAlias.MAX_NAME_LENGTH);

            RuleFor(_request => _request.Xuid)
                .NotEmpty()
                .When(_request => string.IsNullOrEmpty(_request.Name));

            RuleFor(_request => _request.Count)
                .InclusiveBetween(1, 100);

            RuleFor(_request => _request.Offset)
               .GreaterThanOrEqualTo(0);
        }
    }
}
