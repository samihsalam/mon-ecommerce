using System.Xml.Linq;
using MediatR;
using MonEcommerce.Application.Catalogue.Queries;

namespace MonEcommerce.Web.Endpoints;

// RoutePrefix is "" (not "/api/...") — sitemaps are conventionally crawled unprefixed at the site
// root (GET /sitemap.xml), not nested under the API's usual /api/v1 versioning scheme.
public class Sitemap : IEndpointGroup
{
    public static string? RoutePrefix => "";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/sitemap.xml", GetSitemap).AllowAnonymous();
    }

    [EndpointSummary("XML sitemap listing every published product URL, for search-engine crawling")]
    public static async Task<IResult> GetSitemap(ISender sender)
    {
        var entries = await sender.Send(new GetSitemapEntriesQuery());

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(
            ns + "urlset",
            entries.Select(entry => new XElement(
                ns + "url",
                new XElement(ns + "loc", entry.Url),
                new XElement(ns + "lastmod", entry.LastModified.ToString("yyyy-MM-dd")))));

        // XElement construction escapes special characters (&, <, >, etc.) automatically — no
        // manual XML-escaping needed for product names/slugs that might contain them.
        var xml = """<?xml version="1.0" encoding="UTF-8"?>""" + urlset.ToString(SaveOptions.DisableFormatting);
        return Results.Content(xml, "application/xml");
    }
}
