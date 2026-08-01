using MonEcommerce.Application.Common.Utilities;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Common.Utilities;

public class CsvParserTests
{
    [Test]
    public void Parse_ShouldSplitSimpleCommaSeparatedRows()
    {
        var rows = CsvParser.Parse("a,b,c\n1,2,3");

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0], Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(rows[1], Is.EqualTo(new[] { "1", "2", "3" }));
    }

    [Test]
    public void Parse_ShouldHandleQuotedFieldsContainingCommas()
    {
        var rows = CsvParser.Parse("nom,description\n\"Sac, cuir\",\"Un beau sac.\"");

        Assert.That(rows[1], Is.EqualTo(new[] { "Sac, cuir", "Un beau sac." }));
    }

    [Test]
    public void Parse_ShouldUnescapeDoubledQuotesInsideQuotedFields()
    {
        var rows = CsvParser.Parse("nom\n\"Le \"\"meilleur\"\" sac\"");

        Assert.That(rows[1][0], Is.EqualTo("Le \"meilleur\" sac"));
    }

    [Test]
    public void Parse_ShouldSkipBlankLines()
    {
        var rows = CsvParser.Parse("a,b\n\n1,2\n\n");

        Assert.That(rows, Has.Count.EqualTo(2));
    }

    [Test]
    public void Parse_ShouldReturnEmptyListForEmptyContent()
    {
        var rows = CsvParser.Parse("");

        Assert.That(rows, Is.Empty);
    }
}
