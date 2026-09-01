using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Common.Security;
using Domain.Common;
using Domain.Constants;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands.Register;

public record RegisterCommand(
    string Email,
    string FirstName,
    string LastName,
    string UserName,
    string? MobilePhone,
    string Password) : IRequest<Result<int>>;

public class RegisterCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    : IRequestHandler<RegisterCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var isDuplicate = await context.Users
            .AnyAsync(u => u.Email == request.Email || u.UserName == request.UserName, cancellationToken);

        if (isDuplicate)
        {
            return Result.Failure<int>(Error.Conflict("User.DuplicateEmailOrUserName", "Email or username is already registered."));
        }

        // Self-registration always gets the default "User" role - never let the caller pick a role.
        var role = await context.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleNames.User, cancellationToken);

        if (role is null)
        {
            return Result.Failure<int>(Error.EntityNotFound(nameof(Role), RoleNames.User));
        }

        var entity = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserName = request.UserName,
            MobilePhone = request.MobilePhone,
            PasswordHash = passwordHasher.Hash(request.Password),
            RoleId = role.Id
        };

        context.Users.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
