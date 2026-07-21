using MonEcommerce.Application.Common.Utilities;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Common.Utilities;

public class SlugHelperTests
{
    [TestCase("Tote Parisienne en Cuir Cognac", "tote-parisienne-en-cuir-cognac")]
    [TestCase("Écharpe en Laine — Édition Limitée!", "echarpe-en-laine-edition-limitee")]
    [TestCase("  Leading And Trailing Spaces  ", "leading-and-trailing-spaces")]
    [TestCase("Multiple   Consecutive    Spaces", "multiple-consecutive-spaces")]
    [TestCase("Café, Thé & Chocolat", "cafe-the-chocolat")]
    [TestCase("ALREADY-HYPHENATED", "already-hyphenated")]
    public void Slugify_ShouldMatchTheAngularImplementationsKnownOutputs(string input, string expected)
    {
        // These exact input/output pairs are also asserted against the Angular slugify()
        // (product-url.util.ts) — the two algorithms must stay byte-for-byte identical or sitemap
        // URLs silently stop resolving (see Story 3.6's Dev Notes).
        Assert.That(SlugHelper.Slugify(input), Is.EqualTo(expected));
    }
}
