using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Application.Common.Security;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Api.IntegrationTests;


public class AuthenticationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Protected_endpoint_rejects_requests_without_a_token()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/TodoLists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_a_token_signed_with_the_wrong_key()
    {
        var client = factory.CreateClient();

        var fakeToken = CreateToken(
            signingKey: "this-is-a-completely-different-signing-key-not-the-real-one",
            issuer: "MPDCApiTemplate",
            audience: "MPDCApiTemplate");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fakeToken);

        var response = await client.GetAsync("/api/TodoLists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/Auth/login")]
    [InlineData("/api/Auth/register")]
    public async Task Auth_endpoints_are_reachable_without_a_token(string path)
    {
        var client = factory.CreateClient();

        // An empty body fails validation (400), but must never be blocked by the
        // authentication fallback policy (401/403) 
        var response = await client.PostAsync(path, new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Jwt_bearer_scheme_is_registered()
    {
        using var scope = factory.Services.CreateScope();
        var schemeProvider = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        var scheme = await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(scheme);
    }

    [Fact]
    public void Password_hasher_is_registered_and_hashes_roundtrip()
    {
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var hash = hasher.Hash("correct horse battery staple");

        Assert.True(hasher.Verify("correct horse battery staple", hash));
        Assert.False(hasher.Verify("wrong password", hash));
    }

    [Fact]
    public void Jwt_token_generator_is_registered_and_issues_a_token_the_app_would_accept()
    {
        using var scope = factory.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var user = new User
        {
            Id = 1,
            Email = "jane@example.com",
            UserName = "jane",
            FirstName = "Jane",
            LastName = "Doe",
            RoleId = 1,
            Role = new Role { Id = 1, Name = "Admin" }
        };

        var (token, expiresAt) = tokenGenerator.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiresAt > DateTimeOffset.UtcNow);

        // Validate with the same parameters AuthExtension.cs configures, to prove the
        // token this service issues is actually one the running app's JwtBearer handler accepts.
        var jwtSection = configuration.GetSection("Jwt");
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);

        Assert.Equal("1", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Contains(principal.FindAll(ClaimTypes.Role), c => c.Value == "Admin");
    }

    [Fact]
    public async Task A_token_issued_by_the_registered_generator_is_accepted_by_the_running_app()
    {
        using var scope = factory.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var user = new User
        {
            Id = 7,
            Email = "admin@example.com",
            UserName = "admin",
            FirstName = "Admin",
            LastName = "User",
            RoleId = 1,
            Role = new Role { Id = 1, Name = "Admin" }
        };

        var (token, _) = tokenGenerator.GenerateToken(user);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/TodoLists");

        // Reaching the handler (and hitting the database) proves authentication succeeded -
        // anything other than 401/403 here means the token was accepted.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string CreateToken(string signingKey, string issuer, string audience)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, "1")],
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
