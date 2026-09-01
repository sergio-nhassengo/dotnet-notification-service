using Application.Common.Interfaces;
using Domain.Common;
using Domain.Entities;
using MediatR;

namespace Application.Features.Books.Commands.CreateBook;

public record CreateBookCommand(string Name, string Description, string AuthorName, string AuthorEmail) : IRequest<Result<int>>;

public class CreateBookCommandHandler(IApplicationDbContext context) : IRequestHandler<CreateBookCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CreateBookCommand request, CancellationToken cancellationToken)
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
