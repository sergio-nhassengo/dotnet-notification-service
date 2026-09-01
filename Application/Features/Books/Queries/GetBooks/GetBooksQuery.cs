using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Books.Queries.GetBooks;

public record GetBooksQuery : IRequest<Result<List<BookDto>>>;

public class GetBooksQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetBooksQuery, Result<List<BookDto>>>
{
    public async Task<Result<List<BookDto>>> Handle(GetBooksQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Books
            .OrderBy(x => x.Name)
            .ProjectTo<BookDto>(mapperConfiguration)
            .ToListAsync(cancellationToken);

        return result;
    }
}
