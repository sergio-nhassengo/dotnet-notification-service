using Application.Features.Notifications.Commands.CreateEmail;
using Application.Notifications.Idempotency;
using Application.Notifications.Models;

namespace Application.UnitTests.Notifications;

// Request hashes are persisted as idempotency fingerprints. These values were captured from the
// implementation in production; any change to them makes retried requests return 409 Conflict.
public class EmailRequestFingerprintTests
{
    public static CreateEmailNotificationCommand Template() => new("key-1", "corr-1", null, "payment-confirmed",
        new Dictionary<string, string> { ["paymentReference"] = "PAY-123", ["customerName"] = "Customer" }, null)
    {
        To = [new EmailAddress("customer@example.com", "Customer")]
    };

    public static CreateEmailNotificationCommand Raw() => new("key-2", "corr-2", null, null, null, "TIP issued")
    {
        To = [new EmailAddress("one@example.com"), new EmailAddress("two@example.com", "Two")],
        Cc = [new EmailAddress("cc@example.com")],
        HtmlBody = "<p>Truck <b>42</b></p>",
        TextBody = "Truck 42"
    };

    public static CreateEmailNotificationCommand LegacyRecipient() => new("key-3", "corr-3",
        new EmailRecipient("legacy@example.com", "Legacy"), "payment-confirmed",
        new Dictionary<string, string> { ["customerName"] = "Legacy" }, "Override");

    [Theory]
    [InlineData("template", "8714EB0ADF2B06204B8966EB49A68CD161444AE48437EB42720D02889323EA87")]
    [InlineData("raw", "F32BFFF24A0E84C085645B1555B2BF27F730275D4AD98C63EB7C1A83A69388F4")]
    [InlineData("legacy", "DD2CDE1A8518B534A76DB7EB12663EEB8E2DA044751639B45ACEEC1491879DD8")]
    public void Fingerprint_is_stable_for_persisted_requests(string kind, string expected)
    {
        var request = kind switch { "template" => Template(), "raw" => Raw(), _ => LegacyRecipient() };
        Assert.Equal(expected, EmailRequestFingerprint.Compute(request.ToSnapshot()));
    }
}
