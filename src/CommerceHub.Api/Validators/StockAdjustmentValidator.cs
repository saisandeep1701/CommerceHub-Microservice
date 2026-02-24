using FluentValidation;
using CommerceHub.Api.DTOs;

namespace CommerceHub.Api.Validators;

public class StockAdjustmentValidator : AbstractValidator<StockAdjustmentDto>
{
    public StockAdjustmentValidator()
    {
        RuleFor(x => x.Adjustment)
            .NotEqual(0).WithMessage("Adjustment must not be zero.");
    }
}
