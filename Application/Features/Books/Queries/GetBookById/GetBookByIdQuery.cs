using Application.Features.Books.Queries.GetBooks;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Books.Queries.GetBookById;

public record GetBookByIdQuery(int Id) : IRequest<Result<BookDto>>;

public class GetBookByIdQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetBookByIdQuery, Result<BookDto>>
{
    public async Task<Result<BookDto>> Handle(GetBookByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Books
            .Where(x => x.Id == request.Id)
            .ProjectTo<BookDto>(mapperConfiguration)
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? Result.Failure<BookDto>(Error.EntityNotFound(nameof(Domain.Entities.Book), request.Id))
            : result;
    }
}
