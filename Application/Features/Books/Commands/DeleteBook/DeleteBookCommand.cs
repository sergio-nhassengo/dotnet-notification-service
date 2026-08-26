using Application.Common.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Books.Commands.DeleteBook;

public record DeleteBookCommand(int Id) : IRequest;

public class DeleteBookCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteBookCommand>
{
    public async Task Handle(DeleteBookCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Books
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Book), request.Id);
        }

        context.Books.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);
    }
}
