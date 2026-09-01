using Application.Common.Interfaces;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Books.Commands.DeleteBook;

public record DeleteBookCommand(int Id) : IRequest<Result>;

public class DeleteBookCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteBookCommand, Result>
{
    public async Task<Result> Handle(DeleteBookCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Books
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.EntityNotFound(nameof(Domain.Entities.Book), request.Id));
        }

        context.Books.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
