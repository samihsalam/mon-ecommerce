using MonEcommerce.Application.Catalogue.Queries;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Queries;

public class GetProductsQueryValidatorTests
{
    private GetProductsQueryValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new GetProductsQueryValidator();
    }

    [Test]
    public void ShouldBeValidWithNoSearchTerm()
    {
        var result = _validator.Validate(new GetProductsQuery(null, null, null, null, null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldBeValidWithAnEmptySearchTerm()
    {
        var result = _validator.Validate(new GetProductsQuery(null, null, null, null, null, Search: ""));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenSearchTermIsOnlyOneCharacter()
    {
        var result = _validator.Validate(new GetProductsQuery(null, null, null, null, null, Search: "c"));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(GetProductsQuery.Search)), Is.True);
    }

    [Test]
    public void ShouldBeValidWithATwoCharacterSearchTerm()
    {
        var result = _validator.Validate(new GetProductsQuery(null, null, null, null, null, Search: "cu"));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenSearchTermIsPaddedToTwoRawCharactersButOneAfterTrimming()
    {
        // Regression: MinimumLength(2) alone measured the RAW length, but ProductCatalogueService
        // always trims before searching — " a" (raw length 2) previously passed validation and
        // silently became a 1-character search once trimmed, defeating the 2-char floor.
        var result = _validator.Validate(new GetProductsQuery(null, null, null, null, null, Search: " a"));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenSearchTermIsWhitespaceOnly()
    {
        var result = _validator.Validate(new GetProductsQuery(null, null, null, null, null, Search: "   "));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenSearchTermExceedsTheMaximumLength()
    {
        var result = _validator.Validate(new GetProductsQuery(null, null, null, null, null, Search: new string('c', 201)));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldBeValidAtTheMaximumLength()
    {
        var result = _validator.Validate(new GetProductsQuery(null, null, null, null, null, Search: new string('c', 200)));

        Assert.That(result.IsValid, Is.True);
    }
}
