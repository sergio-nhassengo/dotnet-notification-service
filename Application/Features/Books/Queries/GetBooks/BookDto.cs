using AutoMapper;

namespace Application.Features.Books.Queries.GetBooks;

public class BookDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string AuthorName { get; init; } = string.Empty;

    public string AuthorEmail { get; init; } = string.Empty;

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Domain.Entities.Book, BookDto>();
        }
    }
}
