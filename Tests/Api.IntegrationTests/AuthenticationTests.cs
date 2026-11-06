using System.Net;
using System.Net.Http.Headers;

namespace Api.IntegrationTests;

public class AuthenticationTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("Bearer", "unsupported")]
    [InlineData("ApiKey", "otc-api.wrong")]
    [InlineData("ApiKey", "unknown.test-secret")]
    public async Task Invalid_credentials_are_rejected(string? scheme, string? value)
    {
        await using var factory = new NotificationSqliteFactory();
        var client = factory.Client("otc-api");
        client.DefaultRequestHeaders.Authorization = scheme is null ? null : new AuthenticationHeaderValue(scheme, value);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/notifications")).StatusCode);
    }
}
