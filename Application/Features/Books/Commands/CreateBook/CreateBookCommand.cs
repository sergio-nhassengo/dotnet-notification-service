using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Books.Commands.CreateBook;

public record CreateBookCommand(string Name, string Description, string AuthorName, string AuthorEmail) : IRequest<int>;

public class CreateBookCommandHandler(IApplicationDbContext context) : IRequestHandler<CreateBookCommand, int>
{
    public async Task<int> Handle(CreateBookCommand request, CancellationToken cancellationToken)
    {
        var entity = new Book
        {
            Name = request.Name,
            Description = request.Description,
            AuthorName = request.AuthorName,
            AuthorEmail = request.AuthorEmail
        };

        context.Books.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
