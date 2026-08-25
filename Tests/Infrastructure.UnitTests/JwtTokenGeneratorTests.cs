using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Entities;
using Infrastructure.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.UnitTests;

public class JwtTokenGeneratorTests
{
    private const string SigningKey = "this-is-a-test-signing-key-that-is-long-enough";
    private const string Issuer = "MPDCApiTemplate.Tests";
    private const string Audience = "MPDCApiTemplate.Tests";

    private static IConfiguration BuildConfiguration(string? signingKey = SigningKey, int? expirationMinutes = 60)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:SigningKey"] = signingKey
        };

        if (expirationMinutes.HasValue)
        {
            values["Jwt:AccessTokenExpirationMinutes"] = expirationMinutes.Value.ToString();
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static User CreateUser() => new()
    {
        Id = 1,
        Email = "jane@example.com",
        UserName = "jane",
        FirstName = "Jane",
        LastName = "Doe",
        RoleId = 1,
        Role = new Role { Id = 1, Name = "Admin" }
    };

    [Fact]
    public void GenerateToken_issues_a_token_with_the_expected_claims_and_expiry()
    {
        var generator = new JwtTokenGenerator(BuildConfiguration());
        var user = CreateUser();

        var (token, expiresAt) = generator.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.InRange(expiresAt, DateTimeOffset.UtcNow.AddMinutes(59), DateTimeOffset.UtcNow.AddMinutes(61));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);

        Assert.Equal("1", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("jane", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("jane@example.com", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Admin", principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public void GenerateToken_throws_when_SigningKey_is_not_configured()
    {
        var generator = new JwtTokenGenerator(BuildConfiguration(signingKey: null));

        Assert.Throws<InvalidOperationException>(() => generator.GenerateToken(CreateUser()));
    }
}
