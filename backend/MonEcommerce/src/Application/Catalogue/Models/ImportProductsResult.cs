namespace MonEcommerce.Application.Catalogue.Models;

// AC #1's literal JSON sample is { created, errors: [{ row, reason }] } — Warnings is an addition,
// not in that sample, but AC #6 explicitly requires duplicate rows to be "listed as a warning."
public record ImportProductsResult(int Created, List<ImportRowIssue> Errors, List<ImportRowIssue> Warnings);
