using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Utilities;
using MonEcommerce.Domain.Entities;
using AppValidationException = MonEcommerce.Application.Common.Exceptions.ValidationException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class ImportProductsCsvCommandHandler : IRequestHandler<ImportProductsCsvCommand, ImportProductsResult>
{
    private static readonly string[] RequiredColumns = ["nom", "description", "prix", "catégorie", "stock"];

    private readonly IApplicationDbContext _context;

    public ImportProductsCsvCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ImportProductsResult> Handle(ImportProductsCsvCommand request, CancellationToken cancellationToken)
    {
        string content;
        using (var reader = new StreamReader(request.FileContent))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        var rows = CsvParser.Parse(content);
        if (rows.Count == 0)
        {
            return new ImportProductsResult(0, [], []);
        }

        var header = rows[0];
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
        {
            columnIndex[header[i].Trim()] = i;
        }

        var missingColumns = RequiredColumns.Where(c => !columnIndex.ContainsKey(c)).ToList();
        if (missingColumns.Count > 0)
        {
            throw new AppValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "FileContent",
                    $"Colonnes manquantes dans le CSV : {string.Join(", ", missingColumns)}."),
            ]);
        }

        // Loaded once up front, not per row — AC #3's 30s budget for 100 rows would be at risk
        // with N+1 queries against the database for every single row.
        var categoriesByName = await _context.Categories
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var existingNames = await _context.Products
            .Where(p => !p.IsDeleted)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);
        var seenNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var errors = new List<ImportRowIssue>();
        var warnings = new List<ImportRowIssue>();
        var created = 0;

        // Row 1 is the header — row numbers reported back start at 2, matching what an admin sees
        // if they open the file in a spreadsheet app.
        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var fields = rows[i];

            var name = GetField(fields, columnIndex, "nom");
            var description = GetField(fields, columnIndex, "description");
            var priceRaw = GetField(fields, columnIndex, "prix");
            var categoryName = GetField(fields, columnIndex, "catégorie");
            var stockRaw = GetField(fields, columnIndex, "stock");
            var material = GetOptionalField(fields, columnIndex, "matière");
            var color = GetOptionalField(fields, columnIndex, "couleur");

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ImportRowIssue(rowNumber, "Le nom est requis."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(new ImportRowIssue(rowNumber, "La description est requise."));
                continue;
            }

            // Tolerates both "150.00" and French decimal-comma "150,00".
            if (!decimal.TryParse(priceRaw.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var priceInUnits) || priceInUnits <= 0)
            {
                errors.Add(new ImportRowIssue(rowNumber, $"Prix invalide : '{priceRaw}'."));
                continue;
            }

            if (!categoriesByName.TryGetValue(categoryName, out var categoryId))
            {
                errors.Add(new ImportRowIssue(rowNumber, $"Catégorie introuvable : '{categoryName}'."));
                continue;
            }

            if (!int.TryParse(stockRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock) || stock < 0)
            {
                errors.Add(new ImportRowIssue(rowNumber, $"Stock invalide : '{stockRaw}'."));
                continue;
            }

            // AC #6: a product with the same name — whether already in the database or already
            // accepted earlier in this same file — is skipped and listed as a warning, not an error.
            if (seenNames.Contains(name))
            {
                warnings.Add(new ImportRowIssue(rowNumber, $"Produit ignoré, nom déjà existant : '{name}'."));
                continue;
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                PriceInCents = (int)Math.Round(priceInUnits * 100, MidpointRounding.AwayFromZero),
                CategoryId = categoryId,
                Material = material,
                Color = color,
                // AC #5: imported products default to "Dépublié" status.
                IsPublished = false,
            };
            product.Stock = new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Quantity = stock,
            };

            _context.Products.Add(product);
            seenNames.Add(name);
            created++;
        }

        // A single bulk save — invalid/duplicate rows were never added to the change tracker, so
        // there's nothing to roll back (AC #2's "import is not rolled back" is satisfied by
        // construction, not by any explicit transaction handling).
        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ImportProductsResult(created, errors, warnings);
    }

    private static string GetField(string[] fields, IReadOnlyDictionary<string, int> columnIndex, string column)
    {
        var index = columnIndex[column];
        return index < fields.Length ? fields[index].Trim() : string.Empty;
    }

    private static string? GetOptionalField(string[] fields, IReadOnlyDictionary<string, int> columnIndex, string column)
    {
        if (!columnIndex.TryGetValue(column, out var index) || index >= fields.Length)
        {
            return null;
        }

        var value = fields[index].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
