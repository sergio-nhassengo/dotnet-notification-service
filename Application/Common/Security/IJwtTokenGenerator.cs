using System;
using Domain.Entities;

namespace Application.Common.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) GenerateToken(User user);
}
