using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MonEcommerce.Application.Common.Utilities;

// Mirrors frontend/mon-ecommerce-web/src/app/features/catalogue/product-url.util.ts's slugify()
// exactly — lower-case, strip diacritics, replace non-alphanumerics with hyphens, trim. Story
// 3.5's product-detail URL is computed client-side from the product name; this backend copy exists
// solely so GET /sitemap.xml can list URLs that actually resolve to the real routed page. Any
// change to one algorithm MUST be mirrored in the other — nothing enforces this automatically.
public static partial class SlugHelper
{
    public static string Slugify(string text)
    {
        var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var withoutDiacritics = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                withoutDiacritics.Append(c);
            }
        }

        var hyphenated = NonAlphanumericRegex().Replace(withoutDiacritics.ToString(), "-");
        return hyphenated.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();
}
