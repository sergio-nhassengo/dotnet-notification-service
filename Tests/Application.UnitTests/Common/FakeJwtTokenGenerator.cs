using Application.Common.Security;
using Domain.Entities;

namespace Application.UnitTests.Common;

internal sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public string TokenToReturn { get; init; } = "fake-token";

    public DateTimeOffset ExpiresAtToReturn { get; init; } = DateTimeOffset.UtcNow.AddHours(1);

    public User? LastUser { get; private set; }

    public (string Token, DateTimeOffset ExpiresAt) GenerateToken(User user)
    {
        LastUser = user;
        return (TokenToReturn, ExpiresAtToReturn);
    }
}
