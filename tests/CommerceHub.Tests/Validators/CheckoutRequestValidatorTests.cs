using CommerceHub.Api.DTOs;
using CommerceHub.Api.Validators;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace CommerceHub.Tests.Validators;

[TestFixture]
public class CheckoutRequestValidatorTests
{
    private CheckoutRequestValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new CheckoutRequestValidator();
    }

    [Test]
    public void Should_Pass_With_Valid_Request()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-123",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 2 }
            }
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Should_Fail_When_CustomerId_Is_Empty()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 1 }
            }
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerId);
    }

    [Test]
    public void Should_Fail_When_Items_List_Is_Empty()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-123",
            Items = new List<CheckoutItemDto>()
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Test]
    public void Should_Fail_When_Quantity_Is_Zero()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-123",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 0 }
            }
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Test]
    public void Should_Fail_When_Quantity_Is_Negative()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-123",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "prod-1", Quantity = -5 }
            }
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Test]
    public void Should_Fail_When_ProductId_Is_Empty()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-123",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "", Quantity = 1 }
            }
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Test]
    public void Should_Fail_When_Multiple_Items_Have_Invalid_Data()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-123",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "", Quantity = 0 },
                new() { ProductId = "prod-2", Quantity = -1 }
            }
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }
}
