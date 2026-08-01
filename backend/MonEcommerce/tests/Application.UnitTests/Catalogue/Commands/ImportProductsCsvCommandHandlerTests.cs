using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Commands;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppValidationException = MonEcommerce.Application.Common.Exceptions.ValidationException;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class ImportProductsCsvCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private ImportProductsCsvCommandHandler _handler = null!;
    private Guid _categoryId;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var category = new Category { Id = Guid.NewGuid(), Name = "Sacs", Slug = "sacs" };
        _context.Categories.Add(category);
        _categoryId = category.Id;
        _context.SaveChanges();

        _handler = new ImportProductsCsvCommandHandler(_context);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private static Stream ToStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    private const string Header = "nom,description,prix,catégorie,matière,couleur,stock";

    [Test]
    public async Task Handle_ShouldImportAllValidRows()
    {
        var csv = $"{Header}\nSac cuir,Un beau sac.,150.00,Sacs,Cuir,Marron,10\nSac toile,Un sac léger.,45,Sacs,,,3";

        var result = await _handler.Handle(new ImportProductsCsvCommand(ToStream(csv)), CancellationToken.None);

        Assert.That(result.Created, Is.EqualTo(2));
        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Warnings, Is.Empty);

        var products = await _context.Products.Include(p => p.Stock).ToListAsync();
        Assert.That(products, Has.Count.EqualTo(2));

        var sacCuir = products.Single(p => p.Name == "Sac cuir");
        Assert.That(sacCuir.PriceInCents, Is.EqualTo(15000));
        Assert.That(sacCuir.IsPublished, Is.False);
        Assert.That(sacCuir.Stock!.Quantity, Is.EqualTo(10));
        Assert.That(sacCuir.Material, Is.EqualTo("Cuir"));
    }

    [Test]
    public async Task Handle_ShouldAcceptFrenchDecimalCommaPrices()
    {
        var csv = $"{Header}\nSac cuir,Un beau sac.,\"150,00\",Sacs,,,10";

        var result = await _handler.Handle(new ImportProductsCsvCommand(ToStream(csv)), CancellationToken.None);

        Assert.That(result.Created, Is.EqualTo(1));
        var product = await _context.Products.SingleAsync();
        Assert.That(product.PriceInCents, Is.EqualTo(15000));
    }

    [Test]
    public async Task Handle_ShouldReportInvalidRowsWithoutRollingBackValidOnes()
    {
        var csv = $"{Header}\n"
            + "Sac cuir,Un beau sac.,150.00,Sacs,,,10\n" // valid
            + ",Description sans nom,50,Sacs,,,1\n" // missing nom
            + "Sac casse,Description,-10,Sacs,,,1\n" // invalid price
            + "Sac inconnu,Description,50,Inconnue,,,1\n" // unknown category
            + "Sac stock,Description,50,Sacs,,,abc"; // invalid stock

        var result = await _handler.Handle(new ImportProductsCsvCommand(ToStream(csv)), CancellationToken.None);

        Assert.That(result.Created, Is.EqualTo(1));
        Assert.That(result.Errors, Has.Count.EqualTo(4));
        Assert.That(result.Errors.Select(e => e.Row), Is.EqualTo(new[] { 3, 4, 5, 6 }));

        // Only the one valid row was ever persisted — nothing to "roll back".
        Assert.That(await _context.Products.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Handle_ShouldSkipAndWarnOnADuplicateOfAnExistingProduct()
    {
        _context.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Sac cuir",
            Description = "Déjà existant.",
            PriceInCents = 10000,
            CategoryId = _categoryId,
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        var csv = $"{Header}\nSac cuir,Un beau sac.,150.00,Sacs,,,10";

        var result = await _handler.Handle(new ImportProductsCsvCommand(ToStream(csv)), CancellationToken.None);

        Assert.That(result.Created, Is.EqualTo(0));
        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Warnings, Has.Count.EqualTo(1));
        Assert.That(result.Warnings[0].Row, Is.EqualTo(2));

        Assert.That(await _context.Products.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Handle_ShouldSkipAndWarnOnADuplicateWithinTheSameFile()
    {
        var csv = $"{Header}\n"
            + "Sac cuir,Un beau sac.,150.00,Sacs,,,10\n"
            + "Sac cuir,Encore un.,160.00,Sacs,,,5";

        var result = await _handler.Handle(new ImportProductsCsvCommand(ToStream(csv)), CancellationToken.None);

        Assert.That(result.Created, Is.EqualTo(1));
        Assert.That(result.Warnings, Has.Count.EqualTo(1));
        Assert.That(result.Warnings[0].Row, Is.EqualTo(3));
        Assert.That(await _context.Products.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public void Handle_ShouldThrowValidationWhenRequiredColumnsAreMissing()
    {
        var csv = "nom,description\nSac cuir,Un beau sac.";

        Assert.ThrowsAsync<AppValidationException>(async () =>
            await _handler.Handle(new ImportProductsCsvCommand(ToStream(csv)), CancellationToken.None));
    }

    // AC #3: a 100-row file must complete in well under 30s. No real I/O is involved here (EF
    // Core InMemory), so this is a generous ceiling meant to catch an accidental N+1 query
    // regression, not to approach the literal 30s SLA — same approach as Story 5.4's
    // EmailDispatchSlaTests.
    [Test]
    public async Task Handle_ShouldImportOneHundredRowsWellUnderTheSlaCeiling()
    {
        var sb = new StringBuilder(Header);
        for (var i = 0; i < 100; i++)
        {
            sb.Append($"\nProduit {i},Description {i},{10 + i}.00,Sacs,,,{i}");
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await _handler.Handle(new ImportProductsCsvCommand(ToStream(sb.ToString())), CancellationToken.None);
        stopwatch.Stop();

        Assert.That(result.Created, Is.EqualTo(100));
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
    }
}
