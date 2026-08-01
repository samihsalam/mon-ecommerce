using System.Text;

namespace MonEcommerce.Application.Common.Utilities;

// A small, dependency-free RFC-4180-ish CSV parser — comma delimiter, double-quote quoting,
// "" as an escaped quote inside a quoted field. No third-party CSV package is referenced anywhere
// in this codebase yet, and Story 6.3's format needs (nom, description, prix, catégorie, matière,
// couleur, stock) don't justify adding one.
//
// Known, deliberate limitation: reads line-by-line, so a field containing a literal newline
// inside quotes is NOT supported. Acceptable for the free-text fields this parser is used for
// (product name/description/material/color) — flagged here rather than silently wrong.
public static class CsvParser
{
    public static List<string[]> Parse(string content)
    {
        var rows = new List<string[]>();
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            rows.Add(ParseLine(line));
        }

        return rows;
    }

    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}
