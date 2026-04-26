using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common.Interfaces;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Common.Mappings;

public class MappingTests
{
    private IMapper? _mapper;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(IApplicationDbContext).Assembly));
        _mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    [Test]
    public void ShouldHaveValidConfiguration()
    {
        _mapper!.ConfigurationProvider.AssertConfigurationIsValid();
    }
}
