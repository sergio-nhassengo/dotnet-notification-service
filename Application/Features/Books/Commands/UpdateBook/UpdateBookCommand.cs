using Application.Common.Interfaces;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Books.Commands.UpdateBook;

public record UpdateBookCommand(int Id, string Name, string Description, string AuthorName, string AuthorEmail) : IRequest<Result>;

public class UpdateBookCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateBookCommand, Result>
{
    public async Task<Result> Handle(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Books
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.EntityNotFound(nameof(Domain.Entities.Book), request.Id));
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.AuthorName = request.AuthorName;
        entity.AuthorEmail = request.AuthorEmail;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
