using CommerceHub.Api.DTOs;
using CommerceHub.Api.Validators;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace CommerceHub.Tests.Validators;

[TestFixture]
public class StockAdjustmentValidatorTests
{
    private StockAdjustmentValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new StockAdjustmentValidator();
    }

    [Test]
    public void Should_Pass_With_Positive_Adjustment()
    {
        var dto = new StockAdjustmentDto { Adjustment = 10 };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Should_Pass_With_Negative_Adjustment()
    {
        var dto = new StockAdjustmentDto { Adjustment = -5 };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Should_Fail_When_Adjustment_Is_Zero()
    {
        var dto = new StockAdjustmentDto { Adjustment = 0 };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Adjustment);
    }
}
