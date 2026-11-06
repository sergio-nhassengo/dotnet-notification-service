using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Notifications.Abstractions;
using Infrastructure.Notifications.Options;
using Infrastructure.Notifications.Templates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Infrastructure.UnitTests;

public sealed class DomainTemplateTests
{
    public static IEnumerable<object[]> Templates() => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Templates"), "metadata.json", SearchOption.AllDirectories)
        .OrderBy(x => x).Select(path => new object[] { Path.GetFileName(Path.GetDirectoryName(path))! });

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task Domain_template_has_matching_documented_variables_and_renders_both_formats(string id)
    {
        var (metadata, variables) = Read(id);
        using (metadata)
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "Templates", id);
            var required = Regex.Matches(string.Join("", new[] { "subject.txt", "body.html", "body.txt" }.Select(file => File.ReadAllText(Path.Combine(folder, file)))), "\\{\\{([^}]+)\\}\\}")
                .Select(match => match.Groups[1].Value).Distinct().OrderBy(x => x).ToArray();
            Assert.Equal(required, variables.Keys.OrderBy(x => x));
            var rendered = await Renderer().RenderAsync(id, variables, default);
            Assert.DoesNotContain("{{", rendered.Subject + rendered.HtmlBody + rendered.TextBody);
            Assert.InRange(rendered.Subject.Length, 1, 200);
            Assert.DoesNotContain('\n', rendered.Subject);
            Assert.Contains("<html lang=\"en\">", rendered.HtmlBody);
            Assert.Equal(metadata.RootElement.GetProperty("minimumAttachments").GetInt32(), rendered.MinimumAttachments);
            foreach (var value in variables.Values) Assert.Contains(value, rendered.TextBody);
            // Every declared variable really is required, including body-only fields.
            foreach (var key in variables.Keys)
            {
                var incomplete = new Dictionary<string, string>(variables); incomplete.Remove(key);
                var error = await Assert.ThrowsAsync<TemplateException>(() => Renderer().RenderAsync(id, incomplete, default));
                Assert.Contains(key, error.Message);
            }
        }
    }

    [Fact]
    public async Task Values_are_encoded_and_are_not_recursively_interpreted_as_placeholders()
    {
        var variables = new Dictionary<string, string>
        {
            ["consignmentNumber"] = "<script>alert(1)</script>", ["message"] = "{{requestedBy}}",
            ["requestedBy"] = "A & B"
        };
        var rendered = await Renderer().RenderAsync("rail-consignment-alert", variables, default);
        Assert.DoesNotContain("<script>", rendered.HtmlBody);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", rendered.HtmlBody);
        Assert.Contains("{{requestedBy}}", rendered.HtmlBody);
        Assert.Contains("{{requestedBy}}", rendered.TextBody);
        Assert.Contains("A &amp; B", rendered.HtmlBody);
    }

    [Fact]
    public async Task Empty_required_values_and_unknown_templates_are_rejected()
    {
        var (metadata, variables) = Read("tan-tip-payment-confirmed");
        using (metadata)
        {
            variables["paymentReference"] = " ";
            await Assert.ThrowsAsync<TemplateException>(() => Renderer().RenderAsync("tan-tip-payment-confirmed", variables, default));
            await Assert.ThrowsAsync<TemplateException>(() => Renderer().RenderAsync("unknown-template", variables, default));
            await Assert.ThrowsAsync<TemplateException>(() => Renderer().RenderAsync("../outside", variables, default));
        }
    }

    [Fact]
    public async Task Basic_payment_template_still_renders()
    {
        var rendered = await Renderer().RenderAsync("payment-confirmed", new Dictionary<string, string>
        { ["customerName"] = "Ada", ["paymentReference"] = "PAY-1" }, default);
        Assert.Equal("Payment PAY-1 confirmed", rendered.Subject);
        Assert.Equal(0, rendered.MinimumAttachments);
    }

    private static FileEmailTemplateRenderer Renderer()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(AppContext.BaseDirectory);
        return new FileEmailTemplateRenderer(Options.Create(new TemplateOptions()), environment);
    }
    private static (JsonDocument Metadata, Dictionary<string, string> Variables) Read(string id)
    {
        var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Templates", id, "metadata.json")));
        var variables = metadata.RootElement.GetProperty("variables").EnumerateObject()
            .ToDictionary(x => x.Name, x => x.Value.GetProperty("example").GetString()!);
        return (metadata, variables);
    }
}
