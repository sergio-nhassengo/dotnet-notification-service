using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Features.Notifications.Commands.CreateEmail;
using Application.Notifications.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

public sealed class DomainTemplateIntegrationTests
{
    [Fact]
    public async Task All_domain_templates_are_available_only_to_their_documented_applications()
    {
        await using var factory = new NotificationSqliteFactory();
        var applicationIds = new[] { "otc-api", "weighbridge-api", "tan-webservice" };
        var clients = applicationIds.ToDictionary(id => id, factory.Client);
        var attachments = new Dictionary<string, Guid>();
        foreach (var id in applicationIds) attachments[id] = await UploadDocument(clients[id]);
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Templates"), "metadata.json", SearchOption.AllDirectories);
        Assert.Equal(28, files.Length);
        var applications = factory.Services.GetRequiredService<INotificationApplications>();
        var shipped = files.Select(path => Path.GetFileName(Path.GetDirectoryName(path))!).ToHashSet();
        foreach (var id in applicationIds)
            Assert.All(applications.Find(id)!.TemplateIds.Where(template => template != "payment-confirmed"), template => Assert.Contains(template, shipped));

        foreach (var file in files)
        {
            using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(file));
            var templateId = metadata.RootElement.GetProperty("id").GetString()!;
            var variables = metadata.RootElement.GetProperty("variables").EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.GetProperty("example").GetString()!);
            var allowed = metadata.RootElement.GetProperty("applications").EnumerateArray().Select(x => x.GetString()).ToArray();
            var requiresAttachment = metadata.RootElement.GetProperty("minimumAttachments").GetInt32() > 0;
            foreach (var app in applicationIds)
            {
                var request = new CreateEmailNotificationCommand($"{app}:{templateId}", "template-verification", new("recipient@example.com", null),
                    templateId, variables, null, null);
                Assert.Equal(allowed.Contains(app), applications.Find(app)!.TemplateIds.Contains(templateId));
                if (!allowed.Contains(app))
                {
                    Assert.Equal(HttpStatusCode.Unauthorized, (await clients[app].PostAsJsonAsync("/api/v1/notifications/email", request)).StatusCode);
                    continue;
                }
                if (requiresAttachment)
                {
                    Assert.Equal(HttpStatusCode.BadRequest, (await clients[app].PostAsJsonAsync("/api/v1/notifications/email", request)).StatusCode);
                    var logoOnly = request with { Attachments = [new(attachments[app], "logo")] };
                    Assert.Equal(HttpStatusCode.BadRequest, (await clients[app].PostAsJsonAsync("/api/v1/notifications/email", logoOnly)).StatusCode);
                    request = request with { Attachments = [new(attachments[app])] };
                }
                var response = await clients[app].PostAsJsonAsync("/api/v1/notifications/email", request);
                Assert.True(response.StatusCode == HttpStatusCode.Accepted, $"{app}/{templateId}: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }
        }
        Assert.Equal(new[] { "payment-confirmed" }, applications.Find("legacy")!.TemplateIds.Distinct());
    }

    private static async Task<Guid> UploadDocument(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-domain-template-test"));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(bytes, "file", "report.pdf");
        var response = await client.PostAsync("/api/v1/notifications/attachments", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attachmentId").GetGuid();
    }
}
