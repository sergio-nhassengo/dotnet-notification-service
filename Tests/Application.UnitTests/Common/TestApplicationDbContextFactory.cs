using Application.Common.Security;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Persistence;

namespace Application.UnitTests.Common;

internal static class TestApplicationDbContextFactory
{
    public static ApplicationDbContext Create(FakeDateTime? dateTime = null, string? currentUserId = "test-user")
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(currentUserId);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, dateTime ?? new FakeDateTime(DateTimeOffset.UtcNow), currentUserService);
    }

    public static IConfigurationProvider CreateMapperConfiguration() =>
        new MapperConfiguration(cfg => cfg.AddMaps(typeof(global::Application.DependencyInjection).Assembly));
}
