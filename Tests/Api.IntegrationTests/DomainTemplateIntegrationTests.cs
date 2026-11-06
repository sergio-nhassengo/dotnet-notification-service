using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Application.Features.Notifications.Commands.CreateEmail;
using Application.Notifications.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

public sealed class DomainTemplateIntegrationTests
{
    [Fact]
    public async Task All_TAN_templates_are_accepted()
    {
        await using var factory = new NotificationSqliteFactory();
        var applicationIds = new[] { "tan-webservice" };
        var clients = applicationIds.ToDictionary(id => id, factory.Client);
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Templates"), "metadata.json", SearchOption.AllDirectories);
        Assert.Equal(37, files.Length);
        var applications = factory.Services.GetRequiredService<INotificationApplications>();
        var shipped = files.Select(path => Path.GetFileName(Path.GetDirectoryName(path))!).ToHashSet();
        foreach (var id in applicationIds)
            Assert.Equal(shipped.OrderBy(x => x), applications.Find(id)!.TemplateIds.OrderBy(x => x));

        foreach (var file in files)
        {
            using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(file));
            var templateId = metadata.RootElement.GetProperty("id").GetString()!;
            var variables = metadata.RootElement.GetProperty("variables").EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.GetProperty("example").GetString()!);
            var allowed = metadata.RootElement.GetProperty("applications").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Equal(new[] { "tan-webservice" }, allowed);
            foreach (var app in applicationIds)
            {
                var request = new CreateEmailNotificationCommand($"{app}:{templateId}", "template-verification", new("recipient@example.com", null),
                    templateId, variables, null);
                Assert.Equal(allowed.Contains(app), applications.Find(app)!.TemplateIds.Contains(templateId));
                if (!allowed.Contains(app))
                {
                    Assert.Equal(HttpStatusCode.Forbidden, (await clients[app].PostAsJsonAsync("/api/v1/notifications/email", request)).StatusCode);
                    continue;
                }
                var response = await clients[app].PostAsJsonAsync("/api/v1/notifications/email", request);
                Assert.True(response.StatusCode == HttpStatusCode.Accepted, $"{app}/{templateId}: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }
        }
    }

}
