using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Common.Security;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands.Login;

public record LoginCommand(string UserName, string Password) : IRequest<Result<LoginResponse>>;

public class LoginCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IDateTime dateTime) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<LoginResponse>(Error.Unauthorized("Auth.InvalidCredentials", "Invalid username or password."));
        }

        user.LastLogin = dateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = jwtTokenGenerator.GenerateToken(user);

        return new LoginResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            UserName = user.UserName,
            Email = user.Email,
            Role = user.Role.Name
        };
    }
}
