using MediatR;

namespace Application.Features.Books.Queries.GetBooks;

public record GetBooksQuery : IRequest<List<BookDto>>;
