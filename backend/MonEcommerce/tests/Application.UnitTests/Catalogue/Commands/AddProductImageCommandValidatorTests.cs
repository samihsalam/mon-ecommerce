using MonEcommerce.Application.Catalogue.Commands;
using MonEcommerce.Application.Catalogue.Models;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class AddProductImageCommandValidatorTests
{
    private AddProductImageCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new AddProductImageCommandValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        using var stream = new MemoryStream();
        var result = _validator.Validate(new AddProductImageCommand(Guid.NewGuid(), new ProductImageUpload(stream, "photo.jpg")));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenProductIdIsEmpty()
    {
        using var stream = new MemoryStream();
        var result = _validator.Validate(new AddProductImageCommand(Guid.Empty, new ProductImageUpload(stream, "photo.jpg")));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenFileNameIsEmpty()
    {
        using var stream = new MemoryStream();
        var result = _validator.Validate(new AddProductImageCommand(Guid.NewGuid(), new ProductImageUpload(stream, "")));

        Assert.That(result.IsValid, Is.False);
    }
}
