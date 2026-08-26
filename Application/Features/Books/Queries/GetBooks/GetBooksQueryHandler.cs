using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Books.Queries.GetBooks;

public class GetBooksQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetBooksQuery, List<BookDto>>
{
    public Task<List<BookDto>> Handle(GetBooksQuery request, CancellationToken cancellationToken)
    {
        return context.Books
            .OrderBy(x => x.Name)
            .ProjectTo<BookDto>(mapperConfiguration)
            .ToListAsync(cancellationToken);
    }
}
