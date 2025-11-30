using FluentValidation;
using SuledFunctions.Models.Requests;

namespace SuledFunctions.Validators;

/// <summary>
/// Validator for GetTournamentsQuery
/// </summary>
public class GetTournamentsQueryValidator : AbstractValidator<GetTournamentsQuery>
{
    public GetTournamentsQueryValidator()
    {
        RuleFor(x => x.StartDateFrom)
            .LessThanOrEqualTo(x => x.StartDateTo)
            .When(x => x.StartDateFrom.HasValue && x.StartDateTo.HasValue)
            .WithMessage("StartDateFrom must be less than or equal to StartDateTo");

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(1, 500)
            .When(x => x.MaxResults.HasValue)
            .WithMessage("MaxResults must be between 1 and 500");

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .When(x => x.Page.HasValue)
            .WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .When(x => x.PageSize.HasValue)
            .WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.Location)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Location))
            .WithMessage("Location must be 200 characters or less");

        RuleFor(x => x.Division)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Division))
            .WithMessage("Division must be 100 characters or less");
    }
}
