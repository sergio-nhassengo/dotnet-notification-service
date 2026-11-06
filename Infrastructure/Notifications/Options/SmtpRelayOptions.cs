using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Domain.Enums;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Options;

public sealed class SmtpRelayOptions
{
    public string BaseUrl { get; set; } = "https://uatinport.portmaputo.com/api/";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string TokenProperty { get; set; } = "accessToken";
    public int TimeoutSeconds { get; set; } = 30;

    public bool IsValid() => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(CreatedBy) && !string.IsNullOrWhiteSpace(TokenProperty) &&
        TimeoutSeconds is >= 1 and <= 120;
}
