using System;

namespace Application.Features.Auth.Commands.Login;

public class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}
