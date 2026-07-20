using MonEcommerce.Application.Catalogue.Queries;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Queries;

public class GetSearchSuggestionsQueryValidatorTests
{
    private GetSearchSuggestionsQueryValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new GetSearchSuggestionsQueryValidator();
    }

    [Test]
    public void ShouldFailWhenSearchTermIsEmpty()
    {
        var result = _validator.Validate(new GetSearchSuggestionsQuery(""));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenSearchTermIsOnlyOneCharacter()
    {
        var result = _validator.Validate(new GetSearchSuggestionsQuery("c"));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldBeValidWithATwoCharacterSearchTerm()
    {
        var result = _validator.Validate(new GetSearchSuggestionsQuery("cu"));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenSearchTermIsNull()
    {
        var result = _validator.Validate(new GetSearchSuggestionsQuery(null));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenSearchTermIsWhitespaceOnly()
    {
        var result = _validator.Validate(new GetSearchSuggestionsQuery("   "));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenSearchTermIsPaddedToTwoRawCharactersButOneAfterTrimming()
    {
        var result = _validator.Validate(new GetSearchSuggestionsQuery(" a"));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenSearchTermExceedsTheMaximumLength()
    {
        var result = _validator.Validate(new GetSearchSuggestionsQuery(new string('c', 201)));

        Assert.That(result.IsValid, Is.False);
    }
}
